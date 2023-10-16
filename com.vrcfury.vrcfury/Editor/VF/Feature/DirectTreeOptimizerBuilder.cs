using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    public class DirectTreeOptimizerBuilder : FeatureBuilder<DirectTreeOptimizer> {
        [FeatureBuilderAction(FeatureOrder.DirectTreeOptimizer)]
        public void Apply() {
            if (!IsFirst()) return;
            var applyToUnmanaged = allFeaturesInRun
                .OfType<DirectTreeOptimizer>()
                .Any(m => !m.managedOnly);

            var fx = GetFx();
            var applyToLayers = applyToUnmanaged ? fx.GetLayers() : fx.GetManagedLayers();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, layer => GetBindingsAnimatedInLayer(layer));

            var floatTrue = fx.One();
            
            var eligibleLayers = new List<EligibleLayer>();
            var debugLog = new List<string>();
            foreach (var layer in applyToLayers) {
                void AddDebug(string msg) {
                    debugLog.Add($"{layer.name} - {msg}");
                }

                var weight = layer.weight;
                if (!Mathf.Approximately(weight, 1)) {
                    AddDebug($"Not optimizing (layer weight is {weight}, not 1)");
                    continue;
                }

                if (layer.blendingMode == AnimatorLayerBlendingMode.Additive) {
                    AddDebug($"Not optimizing (layer is additive)");
                    continue;
                }

                if (layer.stateMachine.stateMachines.Length > 0) {
                    AddDebug("Not optimizing (contains submachine)");
                    continue;
                }

                var hasBehaviour = new AnimatorIterator.Behaviours().From(layer).Any();
                if (hasBehaviour) {
                    AddDebug($"Not optimizing (contains behaviours)");
                    continue;
                }

                var hasExitTime = new AnimatorIterator.Transitions()
                    .From(layer)
                    .Any(b => b is AnimatorStateTransition t && t.hasExitTime);
                if (hasExitTime) {
                    AddDebug($"Not optimizing (contains a transition using exit time)");
                    continue;
                }
                
                var hasNonZeroTransitonTime = new AnimatorIterator.Transitions()
                    .From(layer)
                    .Any(b => b is AnimatorStateTransition t && t.duration != 0);
                if (hasNonZeroTransitonTime) {
                    AddDebug($"Not optimizing (contains a transition with a non-0 duration)");
                    continue;
                }

                var hasNonstaticClips = new AnimatorIterator.Clips().From(layer)
                    .Any(clip => !ClipBuilderService.IsStaticMotion(clip));

                var usedBindings = bindingsByLayer[layer];
                var otherLayersAnimateTheSameThing = bindingsByLayer
                    .Where(pair => pair.Key != layer && pair.Key.Exists() && pair.Key.GetLayerId() >= layer.GetLayerId() && pair.Value.Any(b => usedBindings.Contains(b)))
                    .Select(pair => pair.Key)
                    .ToArray();
                if (otherLayersAnimateTheSameThing.Length > 0) {
                    var names = string.Join(", ", otherLayersAnimateTheSameThing.Select(l => l.name));
                    AddDebug($"Not optimizing (shares animations with other layer: {names}");
                    continue;
                }

                Motion onClip;
                Motion offClip;
                string param;

                var states = layer.stateMachine.states;
                if (states.Length == 1) {
                    var state = states[0].state;
                    if (hasNonstaticClips && state.timeParameterActive) {
                        // TODO: This could also break if the animation tangents are not linear

                        var dualState = ClipBuilderService.SplitRangeClip(state.motion);
                        if (dualState == null) {
                            AddDebug($"Not optimizing (contains single clip that is not static and not a single time range)");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(state.timeParameter)) {
                            AddDebug($"Not optimizing (uses motion time, but the motion time param is empty)");
                            continue;
                        }

                        offClip = dualState.Item1;
                        offClip.name = state.motion.name + " (OFF)";
                        AssetDatabase.AddObjectToAsset(offClip, state.motion);
                        onClip = dualState.Item2;
                        onClip.name = state.motion.name + " (ON)";
                        AssetDatabase.AddObjectToAsset(onClip, state.motion);
                        param = state.timeParameter;
                    } else {
                        offClip = null;
                        onClip = states[0].state.motion;
                        param = floatTrue.Name();
                    }
                } else {
                    ICollection<AnimatorTransitionBase> GetTransitionsTo(AnimatorState state) {
                        var output = new List<AnimatorTransitionBase>();
                        foreach (var t in new AnimatorIterator.Transitions().From(layer)) {
                            if (t.destinationState == state || (t.isExit && layer.stateMachine.defaultState == state)) {
                                output.Add(t);
                            }
                        }
                        return output.ToArray();
                    }

                    if (states.Length == 3) {
                        bool IsJunkState(AnimatorState state) {
                            return layer.stateMachine.defaultState == state && GetTransitionsTo(state).Count == 0;
                        }
                        states = states.Where(child => !IsJunkState(child.state)).ToArray();
                    }
                    if (states.Length != 2) {
                        AddDebug($"Not optimizing (contains {states.Length} states)");
                        continue;
                    }
                    
                    var state0 = states[0].state;
                    var state1 = states[1].state;

                    var state0Condition = GetSingleCondition(GetTransitionsTo(state0));
                    var state1Condition = GetSingleCondition(GetTransitionsTo(state1));
                    if (state0Condition == null || state1Condition == null) {
                        AddDebug($"Not optimizing (state conditions are not basic)");
                        continue;
                    }
                    if (state0Condition.Value.parameter != state1Condition.Value.parameter) {
                        AddDebug($"Not optimizing (state conditions do not use same parameter)");
                        continue;
                    }

                    var state0EffectiveCondition = EstimateEffectiveCondition(state0Condition.Value);
                    var state1EffectiveCondition = EstimateEffectiveCondition(state1Condition.Value);

                    AnimatorState onState;
                    AnimatorState offState;
                    if (
                        state0EffectiveCondition == EffectiveCondition.WHEN_0 &&
                        state1EffectiveCondition == EffectiveCondition.WHEN_1) {
                        offState = state0;
                        onState = state1;
                    } else if (
                        state0EffectiveCondition == EffectiveCondition.WHEN_1 &&
                        state1EffectiveCondition == EffectiveCondition.WHEN_0) {
                        offState = state1;
                        onState = state0;
                    } else {
                        AddDebug($"Not optimizing (state conditions are not an inversion of each other)");
                        continue;
                    }
                    
                    if (hasNonstaticClips) {
                        Motion GetEffectiveSingleFrameFromNonStatic(AnimatorState s) {
                            if (!(s.motion is AnimationClip clip)) return null;
                            if (s.timeParameterActive) return null;
                            if (clip.isLooping) return null;
                            if (clip.GetLengthInFrames() > 5) return null;
                            var dualState = ClipBuilderService.SplitRangeClip(clip);
                            if (dualState == null) return null;
                            AnimationClip single;
                            if (s.speed >= 0.9) single = dualState.Item2;
                            else if (s.speed <= -0.9) single = dualState.Item1;
                            else if (Mathf.Approximately(s.speed, 0)) single = dualState.Item1;
                            else return null;
                            single.name = $"{clip.name} (speed={s.speed} end state)";
                            AssetDatabase.AddObjectToAsset(single, clip);
                            return single;
                        }

                        offClip = GetEffectiveSingleFrameFromNonStatic(offState);
                        onClip = GetEffectiveSingleFrameFromNonStatic(onState);
                        if (!offClip || !onClip) {
                            AddDebug($"Not optimizing (contains non-static clips)");
                            continue;
                        }
                    } else {
                        offClip = offState.motion;
                        onClip = onState.motion;
                    }
                    
                    param = state0Condition.Value.parameter;
                }

                var paramUsedInOtherLayer = fx.GetLayers()
                    .Where(other => layer != other)
                    .SelectMany(other => new AnimatorIterator.Conditions().From(other))
                    .Any(c => c.parameter == param);

                if (paramUsedInOtherLayer) {
                    AddDebug($"Not optimizing (parameter used in some other layer)");
                    continue;
                }
                
                var paramType = fx.GetRaw().parameters
                    .Where(p => p.name == param)
                    .Select(p => p.type)
                    .DefaultIfEmpty(AnimatorControllerParameterType.Float)
                    .First();
                if (FullControllerBuilder.VRChatGlobalParams.Contains(param) && paramType == AnimatorControllerParameterType.Int) {
                    AddDebug($"Not optimizing (using an int VRC built-in, which means >1 is likely)");
                    continue;
                }

                eligibleLayers.Add(new EligibleLayer {
                    offState = offClip,
                    onState = onClip,
                    param = param
                });

                AddDebug("OPTIMIZING");
                fx.RemoveLayer(layer);
            }
            
            Debug.Log("Optimization report:\n\n" + string.Join("\n", debugLog));

            if (eligibleLayers.Count > 0) {
                var tree = fx.NewBlendTree("Optimized Toggles");
                tree.blendType = BlendTreeType.Direct;
                foreach (var toggle in eligibleLayers) {
                    var offEmpty = ClipBuilderService.IsEmptyMotion(toggle.offState, avatarObject);
                    var onEmpty = ClipBuilderService.IsEmptyMotion(toggle.onState, avatarObject);
                    if (offEmpty && onEmpty) continue;
                    string param;
                    Motion motion;
                    if (!offEmpty) {
                        var subTree = fx.NewBlendTree("Layer " + toggle.offState.name);
                        subTree.useAutomaticThresholds = false;
                        subTree.blendType = BlendTreeType.Simple1D;
                        subTree.AddChild(toggle.offState, 0);
                        subTree.AddChild(
                            !onEmpty ? toggle.onState : fx.GetEmptyClip(), 1);
                        subTree.blendParameter = toggle.param;
                        param = floatTrue.Name();
                        motion = subTree;
                    } else {
                        param = toggle.param;
                        motion = toggle.onState;
                    }

                    tree.AddDirectChild(param, motion);

                    var fxRaw = fx.GetRaw();
                    fxRaw.parameters = fxRaw.parameters.Select(p => {
                        if (p.name == toggle.param) {
                            if (p.type == AnimatorControllerParameterType.Bool) p.defaultFloat = p.defaultBool ? 1 : 0;
                            if (p.type == AnimatorControllerParameterType.Int) p.defaultFloat = p.defaultInt;
                            p.type = AnimatorControllerParameterType.Float;
                        }
                        return p;
                    }).ToArray();
                }
                
                var layer = fx.NewLayer("Optimized Toggles");
                layer.NewState("Optimized Toggles").WithAnimation(tree);
            }
        }

        private ICollection<EditorCurveBinding> GetBindingsAnimatedInLayer(AnimatorStateMachine sm) {
            return new AnimatorIterator.Clips().From(sm)
                .SelectMany(clip => clip.GetAllBindings())
                .Where(binding => binding.IsValid(avatarObject))
                .ToImmutableHashSet();
        }

        public enum EffectiveCondition {
            WHEN_1,
            WHEN_0,
            INVALID
        }
        private static EffectiveCondition EstimateEffectiveCondition(AnimatorCondition cond) {
            if (cond.mode == AnimatorConditionMode.If) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.IfNot) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.Equals && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Equals && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.NotEqual && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.NotEqual && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Greater && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Less && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.Less && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_0;
            return EffectiveCondition.INVALID;
        }

        private static AnimatorCondition? GetSingleCondition(IEnumerable<AnimatorTransitionBase> transitions) {
            var allConditions = transitions
                .SelectMany(t => t.conditions)
                .Distinct()
                .ToList();
            if (allConditions.Count != 1) return null;
            return allConditions[0];
        }

        public class EligibleLayer {
            public Motion offState;
            public Motion onState;
            public string param;
        }
        
        public override string GetEditorTitle() {
            return "Direct Tree Optimizer";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically convert all non-conflicting toggle layers into a single direct blend tree layer." +
                "\n\nWarning: Toggles may not work in Av3 emulator when using this feature. This is a bug in Av3 emulator. Use Gesture Manager for testing instead."
            ));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
    }
}