using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace VF.Feature {
    public class DirectTreeOptimizerBuilder : FeatureBuilder<DirectTreeOptimizer> {
        [FeatureBuilderAction(FeatureOrder.DirectTreeOptimizer)]
        public void Apply() {
            var fx = GetFx();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, layer => GetBindingsAnimatedInLayer(layer));

            var floatTrue = fx.NewFloat("floatTrue", def: 1);
            
            var eligibleLayers = new List<EligibleLayer>();
            var debugLog = new List<string>();
            foreach (var (layer,i) in fx.GetLayers().Select((layer,i) => (layer,i))) {
                void AddDebug(string msg) {
                    debugLog.Add($"{layer.name} - {msg}");
                }

                var weight = fx.GetWeight(layer);
                if (!Mathf.Approximately(weight, 1)) {
                    AddDebug($"Not optimizing (layer weight is {weight}, not 1)");
                    continue;
                }

                if (layer.stateMachines.Length > 0) {
                    AddDebug("Not optimizing (contains submachine)");
                    continue;
                }

                var hasBehaviour = false;
                AnimatorIterator.ForEachBehaviour(layer, b => {
                    hasBehaviour = true;
                });
                if (hasBehaviour) {
                    AddDebug($"Not optimizing (contains behaviours)");
                    continue;
                }

                var hasNonstaticClips = false;
                AnimatorIterator.ForEachClip(layer, clip => {
                    hasNonstaticClips |= !ClipBuilder.IsStaticMotion(clip);
                });

                var usedBindings = bindingsByLayer[layer];
                var otherLayersAnimateTheSameThing = bindingsByLayer
                    .Where(pair => pair.Key != layer && pair.Value.Any(b => usedBindings.Contains(b)))
                    .Select(pair => pair.Key)
                    .ToArray();
                if (otherLayersAnimateTheSameThing.Length > 0) {
                    var names = string.Join(", ", otherLayersAnimateTheSameThing.Select(l => fx.GetLayerName(l)));
                    AddDebug($"Not optimizing (shares animations with other layer: {names}");
                    continue;
                }

                Motion onClip;
                Motion offClip;
                string param;

                var states = layer.states;
                if (states.Length == 1) {
                    var state = states[0].state;
                    if (hasNonstaticClips) {
                        var dualState = ClipBuilder.SplitRangeClip(state.motion);
                        if (dualState == null) {
                            AddDebug($"Not optimizing (contains single clip that is not static and not a single time range)");
                            continue;
                        }
                        if (!state.timeParameterActive || string.IsNullOrWhiteSpace(state.timeParameter)) {
                            AddDebug($"Not optimizing (contains a time range clip but doesn't use motion time)");
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
                        AnimatorIterator.ForEachTransition(layer, t => {
                            if (t.destinationState == state || (t.isExit && layer.defaultState == state)) {
                                output.Add(t);
                            }
                        });
                        return output.ToArray();
                    }

                    if (states.Length == 3) {
                        bool IsJunkState(AnimatorState state) {
                            return layer.defaultState == state && GetTransitionsTo(state).Count == 0;
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
                            if (ClipBuilder.GetLengthInFrames(clip) > 5) return null;
                            var dualState = ClipBuilder.SplitRangeClip(clip);
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

                var paramUsedInOtherLayer = false;
                foreach (var other in fx.GetLayers()) {
                    AnimatorIterator.ForEachTransition(other, t => {
                        paramUsedInOtherLayer |= layer != other && t.conditions.Any(c => c.parameter == param);
                    });
                }

                if (paramUsedInOtherLayer) {
                    AddDebug($"Not optimizing (parameter used in some other layer)");
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
                    var offEmpty = ClipBuilder.IsEmptyMotion(toggle.offState, avatarObject);
                    var onEmpty = ClipBuilder.IsEmptyMotion(toggle.onState, avatarObject);
                    if (offEmpty && onEmpty) continue;
                    string param;
                    Motion motion;
                    if (!offEmpty) {
                        var subTree = fx.NewBlendTree("Layer " + toggle.offState.name);
                        subTree.useAutomaticThresholds = false;
                        subTree.blendType = BlendTreeType.Simple1D;
                        subTree.AddChild(toggle.offState, 0);
                        subTree.AddChild(
                            !onEmpty ? toggle.onState : fx.GetNoopClip(), 1);
                        subTree.blendParameter = toggle.param;
                        param = floatTrue.Name();
                        motion = subTree;
                    } else {
                        param = toggle.param;
                        motion = toggle.onState;
                    }

                    tree.AddChild(motion);
                    var children = tree.children;
                    var child = children[children.Length - 1];
                    child.directBlendParameter = param;
                    children[children.Length - 1] = child;
                    tree.children = children;

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
            var usedBindings = new HashSet<EditorCurveBinding>();
            AnimatorIterator.ForEachClip(sm, clip => {
                usedBindings.UnionWith(AnimationUtility.GetCurveBindings(clip).Where(c => !c.path.Contains("_ignored")));
                usedBindings.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
            });
            usedBindings.RemoveWhere(binding => binding.path == "_ignored");
            return usedBindings;
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
    }
}