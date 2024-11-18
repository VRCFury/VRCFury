using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class LayerToTreeService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AnimatorLayerControlOffsetService layerControlService;
        [VFAutowired] private readonly FixWriteDefaultsService fixWriteDefaults;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        [FeatureBuilderAction(FeatureOrder.LayerToTree)]
        public void Apply() {
            var applyToUnmanaged = globals.allFeaturesInRun
                .OfType<DirectTreeOptimizer>()
                .Any();

            var applyToLayers = applyToUnmanaged ? fx.GetLayers() : fx.GetManagedLayers();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, GetBindingsAnimatedInLayer);

            var directTree = new Lazy<VFBlendTreeDirect>(() => directTreeService.Create());
            
            var debugLog = new List<string>();
            foreach (var layer in applyToLayers) {
                try {
                    OptimizeLayer(layer, bindingsByLayer, directTree);
                    debugLog.Add($"{layer.name} - OPTIMIZED");
                    layer.Remove();
                } catch (DoNotOptimizeException e) {
                    debugLog.Add($"{layer.name} - Not Optimizing ({e.Message})");
                }
            }
            
            Debug.Log("Optimization report:\n\n" + debugLog.Join('\n'));
        }

        private void OptimizeLayer(VFLayer layer, Dictionary<VFLayer, ICollection<EditorCurveBinding>> bindingsByLayer, Lazy<VFBlendTreeDirect> directTree) {
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
            
            if (layerControlService.IsLayerTargeted(layer)) {
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
                var names = otherLayersAnimateTheSameThing.Select(l => l.name).Join(", ");
                throw new DoNotOptimizeException($"Shares animations with other layer: {names}");
            }

            if (states.Length == 1) {
                var state = states[0].state;
                var onClip = Make0LengthClipForState(layer, state);
                if (onClip != null) {
                    directTree.Value.Add(onClip);
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
            
            var state0Clip = Make0LengthClipForState(layer, state0);
            var state1Clip = Make0LengthClipForState(layer, state1);

            Optimize(state0Condition.Value, state0Clip, state1Clip, directTree);
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

        private void Optimize(AnimatorCondition condition, Motion on, Motion off, Lazy<VFBlendTreeDirect> directTree) {
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
                    directTree.Value.Add(param, on);
                } else {
                    directTree.Value.Add(BlendtreeMath.GreaterThan(param, 0).create(on, off));
                }
            } else if (condition.mode == AnimatorConditionMode.Equals) {
                directTree.Value.Add(BlendtreeMath.Equals(param, condition.threshold).create(on, off));
            } else if (condition.mode == AnimatorConditionMode.Greater) {
                directTree.Value.Add(BlendtreeMath.GreaterThan(param, condition.threshold).create(on, off));
            } else {
                throw new DoNotOptimizeException($"Unknown condition type");
            }
        }

        [CanBeNull]
        private Motion Make0LengthClipForState(VFLayer layer, AnimatorState state) {
            if (state.motion == null) return null;

            if (state.motion.IsStatic()) {
                return state.motion.GetLastFrame();
            }

            if (!state.motion.IsTwoState()) {
                throw new DoNotOptimizeException($"{state.name} contains a non-static clip with more than 2 keyframes");
            }

            var clipsInMotion = new AnimatorIterator.Clips().From(state.motion);
            if (clipsInMotion.Any(clip => clip.isLooping)) {
                throw new DoNotOptimizeException($"{state.name} contains non-static motion that loops");
            }

            var hasNegativeTimeScaleClip = new AnimatorIterator.Trees().From(state.motion)
                .SelectMany(tree => tree.children)
                .Any(child => child.timeScale <= 0);
            if (hasNegativeTimeScaleClip) {
                throw new DoNotOptimizeException($"{state.name} contains a tree child with a timeScale <= 0");
            }

            var startMotion = state.motion.GetLastFrame(false);
            var endMotion = state.motion.GetLastFrame(true);

            if (state.timeParameterActive) {
                // TODO: This could also break if the animation tangents are not linear
                if (string.IsNullOrWhiteSpace(state.timeParameter)) {
                    throw new DoNotOptimizeException($"{state.name} contains a motion time clip without a valid parameter");
                }
                var subTree = VFBlendTree1D.Create($"Layer {layer.name} - {state.name}", state.timeParameter);
                subTree.Add(0, startMotion);
                subTree.Add(1, endMotion);
                return subTree;
            } else if (state.motion is AnimationClip) {
                if (clipsInMotion.Any(clip => clip.GetLengthInFrames() > 5)) {
                    throw new DoNotOptimizeException($"{state.name} contains a non-static clip that is long enough to notice the animation");
                }
                if (state.speed >= 0.9) return endMotion;
                if (state.speed <= -0.9) return startMotion;
                if (Mathf.Approximately(state.speed, 0)) return startMotion;
                throw new DoNotOptimizeException($"{state.name} contains a non-static clip with a non-standard speed");
            } else {
                throw new DoNotOptimizeException($"{state.name} contains a non-static clip that is a blendtree not using motion time");
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
    }
}