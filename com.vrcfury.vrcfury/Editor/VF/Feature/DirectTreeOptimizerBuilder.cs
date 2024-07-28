using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {
    internal class DirectTreeOptimizerBuilder : FeatureBuilder<DirectTreeOptimizer> {
        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder layerControlBuilder;
        [VFAutowired] private readonly FixWriteDefaultsBuilder fixWriteDefaults;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly MathService math;
        
        [FeatureBuilderAction(FeatureOrder.DirectTreeOptimizer)]
        public void Apply() {
            if (!IsFirst()) return;
            var applyToUnmanaged = allFeaturesInRun
                .OfType<DirectTreeOptimizer>()
                .Any(m => !m.managedOnly);

            var applyToLayers = applyToUnmanaged ? fx.GetLayers() : fx.GetManagedLayers();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, GetBindingsAnimatedInLayer);
            
            var debugLog = new List<string>();
            foreach (var layer in applyToLayers) {
                try {
                    OptimizeLayer(layer, bindingsByLayer);
                    debugLog.Add($"{layer.name} - OPTIMIZED");
                    layer.Remove();
                } catch (DoNotOptimizeException e) {
                    debugLog.Add($"{layer.name} - Not Optimizing ({e.Message})");
                }
            }
            
            Debug.Log("Optimization report:\n\n" + string.Join("\n", debugLog));
        }

        private void OptimizeLayer(VFLayer layer, Dictionary<VFLayer, ICollection<EditorCurveBinding>> bindingsByLayer) {
            // We must never optimize the defaults layer.
            // While it may seem impossible for the defaults layer to be optimized (because it shares keys
            // with other layers), it's theoretically possible for the layer to be created early with bindings
            // that are no longer valid, making it "empty" at this point, and if we direct tree optimize it,
            // the layer will be missing later when the FixWriteDefaultBuilder tries to add to it.
            if (layer == fixWriteDefaults.GetDefaultLayer()) {
                throw new DoNotOptimizeException($"This is the vrcf defaults layer");
            }

            var weight = layer.weight;
            if (!Mathf.Approximately(weight, 1)) {
                throw new DoNotOptimizeException($"Layer weight is {weight}, not 1");
            }

            if (layer.blendingMode == AnimatorLayerBlendingMode.Additive) {
                throw new DoNotOptimizeException($"Layer is additive");
            }
            
            if (layerControlBuilder.IsLayerTargeted(layer)) {
                throw new DoNotOptimizeException($"Layer is targeted by an Animator Layer Control");
            }

            if (layer.stateMachine.stateMachines.Length > 0) {
                throw new DoNotOptimizeException("Contains submachine");
            }

            var hasBehaviour = new AnimatorIterator.Behaviours().From(layer).Any();
            if (hasBehaviour) {
                throw new DoNotOptimizeException($"Contains behaviours");
            }

            var hasExitTime = new AnimatorIterator.Transitions()
                .From(layer)
                .Any(b => b is AnimatorStateTransition t && t.hasExitTime);
            if (hasExitTime) {
                throw new DoNotOptimizeException($"Contains a transition using exit time");
            }
            
            var hasNonZeroTransitonTime = new AnimatorIterator.Transitions()
                .From(layer)
                .Any(b => b is AnimatorStateTransition t && t.duration != 0);
            if (hasNonZeroTransitonTime) {
                throw new DoNotOptimizeException($"Contains a transition with a non-0 duration");
            }

            var states = layer.stateMachine.states;
            states = states.Where(child => !IsUnreachableState(layer, child.state)).ToArray();

            var hasEulerRotation = states.Any(state => {
                if (state.state.motion is BlendTree) return false;
                return new AnimatorIterator.Clips().From(state.state.motion)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Where(binding => binding.IsValid(avatarObject))
                    .Select(binding => binding.Normalize(true))
                    .Any(b => b.propertyName == EditorCurveBindingExtensions.NormalizedRotationProperty);
            });
            if (hasEulerRotation) {
                throw new DoNotOptimizeException($"Animates transform rotations, which work differently within blend trees");
            }
            
            var usedBindings = bindingsByLayer[layer];
            var otherLayersAnimateTheSameThing = bindingsByLayer
                .Where(pair => pair.Key != layer) // It's not the current layer
                .Where(pair => pair.Key.Exists()) // The other layer hasn't been deleted
                .Where(pair => pair.Key.GetLayerId() >= layer.GetLayerId()) // The other layer has higher priority
                .Where(pair => pair.Value.Any(b => usedBindings.Contains(b))) // The other layer animates the same thing we do
                .Select(pair => pair.Key)
                .ToArray();
            if (otherLayersAnimateTheSameThing.Length > 0) {
                var names = string.Join(", ", otherLayersAnimateTheSameThing.Select(l => l.name));
                throw new DoNotOptimizeException($"Shares animations with other layer: {names}");
            }

            if (states.Length == 1) {
                var state = states[0].state;
                var onClip = MakeClipForState(layer, state);
                if (onClip != null) {
                    directTree.Add(onClip);
                }
                return;
            }
            if (states.Length == 3) {
                states = states.Where(child => !IsEntryOnlyState(layer, child.state)).ToArray();
            }
            if (states.Length != 2) {
                throw new DoNotOptimizeException($"Contains {states.Length} states");
            }
            
            var state0 = states[0].state;
            var state1 = states[1].state;

            var state0Condition = GetSingleCondition(GetTransitionsTo(layer, state0));
            var state1Condition = GetSingleCondition(GetTransitionsTo(layer, state1));
            if (state0Condition == null || state1Condition == null) {
                throw new DoNotOptimizeException($"State conditions are not basic");
            }
            if (state0Condition.Value.parameter != state1Condition.Value.parameter) {
                throw new DoNotOptimizeException($"State conditions do not use same parameter");
            }
            
            var param = new VFAFloat(state0Condition.Value.parameter, 0);
            var paramType = fx.GetRaw().parameters
                .Where(p => p.name == param)
                .Select(p => p.type)
                .DefaultIfEmpty(AnimatorControllerParameterType.Float)
                .First();
            if (FullControllerBuilder.VRChatGlobalParams.Contains(param) && paramType == AnimatorControllerParameterType.Int) {
                throw new DoNotOptimizeException($"Uses an int VRC built-in, which means >1 is likely");
            }
            
            // TODO: Might want to verify that state1Condition is the opposite of state0Condition
            // But we already verify that they use the same parameter, so it's /extremely/ unlikely for this to not be the case
            
            var state0Clip = MakeClipForState(layer, state0);
            var state1Clip = MakeClipForState(layer, state1);

            Optimize(state0Condition.Value, state0Clip, state1Clip);
        }
        
        private static bool IsEntryOnlyState(VFLayer layer, AnimatorState state) {
            return layer.stateMachine.defaultState == state && GetTransitionsTo(layer, state).Count == 0;
        }
        
        private static bool IsUnreachableState(VFLayer layer, AnimatorState state) {
            return layer.stateMachine.defaultState != state && GetTransitionsTo(layer, state).Count == 0;
        }
        
        private static ICollection<AnimatorTransitionBase> GetTransitionsTo(VFLayer layer, AnimatorState state) {
            var output = new List<AnimatorTransitionBase>();
            var ignoreTransitions = new HashSet<AnimatorTransitionBase>();
            var entryState = layer.stateMachine.defaultState;

            if (layer.stateMachine.entryTransitions.Length == 1 &&
                layer.stateMachine.entryTransitions[0].conditions.Length == 0) {
                entryState = layer.stateMachine.entryTransitions[0].destinationState;
                ignoreTransitions.Add(layer.stateMachine.entryTransitions[0]);
            }

            foreach (var t in new AnimatorIterator.Transitions().From(layer)) {
                if (ignoreTransitions.Contains(t)) continue;
                if (t.destinationState == state || (t.isExit && entryState == state)) {
                    output.Add(t);
                }
            }
            return output.ToArray();
        }

        private void Optimize(AnimatorCondition condition, Motion on, Motion off) {
            if (on == null) on = clipFactory.GetEmptyClip();
            if (off == null) off = clipFactory.GetEmptyClip();
            
            if (condition.mode == AnimatorConditionMode.IfNot) {
                condition.mode = AnimatorConditionMode.If;
                (on, off) = (off, on);
            } else if (condition.mode == AnimatorConditionMode.Less) {
                condition.mode = AnimatorConditionMode.Greater;
                condition.threshold = VRCFuryEditorUtils.NextFloatDown(condition.threshold);
                (on, off) = (off, on);
            } else if (condition.mode == AnimatorConditionMode.NotEqual) {
                condition.mode = AnimatorConditionMode.Equals;
                (on, off) = (off, on);
            }

            var onValid = on.HasValidBinding(avatarObject);
            var offValid = off.HasValidBinding(avatarObject);
            if (!onValid && !offValid) throw new DoNotOptimizeException($"Contains no valid bindings");

            var param = new VFAFloat(condition.parameter, 0);
            if (condition.mode == AnimatorConditionMode.If) {
                if (!offValid) {
                    directTree.Add(param, on);
                } else {
                    directTree.Add(math.GreaterThan(param, 0.5f).create(on, off));
                }
            } else if (condition.mode == AnimatorConditionMode.Equals) {
                directTree.Add(math.Equals(param, condition.threshold).create(on, off));
            } else if (condition.mode == AnimatorConditionMode.Greater) {
                directTree.Add(math.GreaterThan(param, condition.threshold).create(on, off));
            } else {
                throw new DoNotOptimizeException($"Unknown condition type");
            }
        }

        private Motion MakeClipForState(VFLayer layer, AnimatorState state) {
            var hasNonstaticClips = new AnimatorIterator.Clips().From(state).Any(clip => !clip.IsStatic());
            if (hasNonstaticClips) {
                if (!(state.motion is AnimationClip clip)) {
                    throw new DoNotOptimizeException($"{state.name} contains a blendtree that is not static");
                }
                if (clip.isLooping) {
                    throw new DoNotOptimizeException($"{state.name} contains non-static clip set to loop");
                }

                var dualState = ClipBuilderService.SplitRangeClip(clip);
                if (dualState == null) {
                    throw new DoNotOptimizeException($"{state.name} contains a non-static clip with more than 2 keyframes");
                }

                if (state.timeParameterActive) {
                    // TODO: This could also break if the animation tangents are not linear
                    if (string.IsNullOrWhiteSpace(state.timeParameter)) {
                        throw new DoNotOptimizeException($"{state.name} contains a motion time clip without a valid parameter");
                    }
                    var subTree = clipFactory.New1D($"Layer {layer.name} - {state.name}", state.timeParameter);
                    subTree.Add(0, dualState.Item1);
                    subTree.Add(1, dualState.Item2);
                    return subTree;
                } else {
                    if (clip.GetLengthInFrames() > 5) {
                        throw new DoNotOptimizeException($"{state.name} contains a non-static clip that is long enough to notice the animation");
                    }
                    AnimationClip single;
                    if (state.speed >= 0.9) single = dualState.Item2;
                    else if (state.speed <= -0.9) single = dualState.Item1;
                    else if (Mathf.Approximately(state.speed, 0)) single = dualState.Item1;
                    else {
                        throw new DoNotOptimizeException($"{state.name} contains a non-static clip with a non-standard speed");
                    }
                    return single;
                }
            } else {
                return state.motion;
            }
        }

        class DoNotOptimizeException : Exception {
            public DoNotOptimizeException(string message) : base(message) {
            }
        }

        private ICollection<EditorCurveBinding> GetBindingsAnimatedInLayer(VFLayer layer) {
            return new AnimatorIterator.Clips().From(layer)
                .SelectMany(clip => clip.GetAllBindings())
                .Where(binding => binding.IsValid(avatarObject))
                .Select(binding => binding.Normalize(true))
                .ToImmutableHashSet();
        }

        private static AnimatorCondition? GetSingleCondition(IEnumerable<AnimatorTransitionBase> transitions) {
            var allConditions = transitions
                .SelectMany(t => t.conditions)
                .Distinct()
                .ToList();
            if (allConditions.Count != 1) return null;
            return allConditions[0];
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

        public override bool AvailableOnRootOnly() {
            return true;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
    }
}
