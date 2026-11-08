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
using VF.Menu;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class LayerToTreeService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AnimatorLayerControlOffsetService layerControlService;
        [VFAutowired] private readonly FixWriteDefaultsService fixWriteDefaults;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController fx => controllers.GetFx();
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ValidateBindingsService validateBindingsService;
        [VFAutowired] private readonly LayerSourceService layerSourceService;
        [VFAutowired] private readonly CleanupEmptyLayersService cleanupEmptyLayers;

        private VFAParam GetExistingParameter(string name) {
            if (!fx.TryGetParam(name, out var parameter)) throw new DoNotOptimizeException($"Uses undeclared parameter {name}");
            return parameter;
        }

        [FeatureBuilderAction(FeatureOrder.LayerToTree)]
        public void Apply() {
            if (DisableDbtOptimizerMenuItem.Get() && Application.isPlaying) {
                return;
            }

            var applyToUnmanaged = globals.allFeaturesInRun
                .OfType<DirectTreeOptimizer>()
                .Any();

            var applyToLayers = applyToUnmanaged ? fx.GetLayers() : fx.GetManagedLayers();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, GetBindingsAnimatedInLayer);
            var layersByBinding = new Dictionary<VFBinding, HashSet<VFLayer>>();
            foreach (var pair in bindingsByLayer) {
                foreach (var binding in pair.Value) {
                    layersByBinding.GetOrCreate(binding, () => new HashSet<VFLayer>()).Add(pair.Key);
                }
            }

            var directTree = new Lazy<VFBlendTreeDirect>(() => directTreeService.Create());
            
            var debugLog = new List<string>();
            foreach (var layer in applyToLayers) {
                try {
                    OptimizeLayer(layer, bindingsByLayer, layersByBinding, directTree);
                    debugLog.Add($"{layer.name} - OPTIMIZED");
                    layer.Remove();
                } catch (DoNotOptimizeException e) {
                    debugLog.Add($"{layer.name} - Not Optimizing ({e.Message})");
                }
            }
            
            Debug.Log("Optimization report:\n\n" + debugLog.Join('\n'));
        }

        private void OptimizeLayer(
            VFLayer layer,
            Dictionary<VFLayer, ICollection<VFBinding>> bindingsByLayer,
            Dictionary<VFBinding, HashSet<VFLayer>> layersByBinding,
            Lazy<VFBlendTreeDirect> directTree
        ) {
            if (cleanupEmptyLayers.WouldRemove(layer)) {
                throw new DoNotOptimizeException("Contains no valid animations (VRCF would delete this layer during a normal upload)");
            }

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

            if (layer.hasSubMachines) {
                throw new DoNotOptimizeException("Contains submachine");
            }

            var hasBehaviour = layer.HasBehaviours();
            if (hasBehaviour) {
                throw new DoNotOptimizeException($"Contains behaviours");
            }

            var hasExitTime = layer.allTransitions
                .OfType<VFTransition>()
                .Any(t => t.hasExitTime);
            if (hasExitTime) {
                throw new DoNotOptimizeException($"Contains a transition using exit time");
            }

            var hasNonZeroTransitonTime = layer.allTransitions
                .OfType<VFTransition>()
                .Any(t => t.duration != 0);
            if (hasNonZeroTransitonTime) {
                throw new DoNotOptimizeException($"Contains a transition with a non-0 duration");
            }

            var states = layer.allStates.ToArray();
            states = states.Where(state => !IsUnreachableState(layer, state)).ToArray();

            var hasEulerRotation = states.Any(state => {
                // Technically if this state contains a blendtree, it should be safe to optimize anyways since
                // the rotation would already be quaternion based anyways. HOWEVER, due to the way action loading works,
                // toggles are ALWAYS a blend tree (containing a bunch of things with always-1 weights), and they may be
                // flattened down to a single clip later on. This means it's unsafe for us to optimize rotations in a blendtree here.
                // If we didn't create the layer (it came from the descriptor or a full controller), then it should still be safe
                // to optimize.
                if (state.motion is VFTree && !layerSourceService.DidCreate(layer)) return false;
                return new AnimatorIterator.Clips().From(state.motion)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Where(binding => validateBindingsService.IsValid(binding, avatarObject))
                    .Select(binding => binding.Normalize(true))
                    .Any(b => b.propertyName == VFBinding.NormalizedRotationProperty);
            });
            if (hasEulerRotation) {
                throw new DoNotOptimizeException($"Animates transform rotations, which work differently within blend trees");
            }
            
            var usedBindings = bindingsByLayer[layer];
            if (!layer.TryGetLayerId(out var layerId)) {
                throw new DoNotOptimizeException("Layer was removed");
            }
            var layersWithMatchingBindings = new HashSet<VFLayer>();
            foreach (var binding in usedBindings) {
                if (layersByBinding.TryGetValue(binding, out var matchingLayers)) {
                    layersWithMatchingBindings.UnionWith(matchingLayers);
                }
            }
            var otherLayersAnimateTheSameThing = layersWithMatchingBindings
                .Where(otherLayer => otherLayer != layer)
                // Only fail if the other layer has higher priority
                .Where(otherLayer => otherLayer.TryGetLayerId(out var otherLayerId) && otherLayerId >= layerId)
                .ToArray();
            if (otherLayersAnimateTheSameThing.Length > 0) {
                var names = otherLayersAnimateTheSameThing.Select(l => l.name).Join(", ");
                throw new DoNotOptimizeException($"Shares animations with other layer: {names}");
            }

            if (states.Length == 1) {
                var state = states[0];
                var onClip = Make0LengthClipForState(layer, state);
                if (onClip != null) {
                    directTree.Value.Add(onClip);
                }
                return;
            }
            if (states.Length == 3) {
                states = states.Where(state => !IsEntryOnlyState(layer, state)).ToArray();
            }
            if (states.Length != 2) {
                throw new DoNotOptimizeException($"Contains {states.Length} states");
            }
            
            var state0 = states[0];
            var state1 = states[1];

            var to0 = GetTransitionsTo(layer, state0).Select(t => t.conditions).ToArray();
            var to1 = GetTransitionsTo(layer, state1).Select(t => t.conditions).ToArray();
            // Negate the smaller expression to avoid expanding an already-expanded opposite.
            var (toCheck, toNegate) = to0.Sum(t => t.Length) >= to1.Sum(t => t.Length) ? (to0, to1) : (to1, to0);
            if (toNegate.Sum(t => t.Length) > 6) {
                throw new DoNotOptimizeException("Contains more than six conditions before negation");
            }
            var conditions = new HashSet<HashSet<AnimatorCondition>>(
                toCheck.Select(t => t.ToHashSet()), HashSet<AnimatorCondition>.CreateSetComparer());
            if (!conditions.SetEquals(AnimatorConditionLogic.Not(toNegate).Select(t => t.ToHashSet()))) {
                // Allow some not-exactly-opposite conditions to still be optimized
                // These are some common cases that creators make toggles which aren't exactly "opposite"
                var state0Condition = GetSingleCondition(GetTransitionsTo(layer, state0));
                var state1Condition = GetSingleCondition(GetTransitionsTo(layer, state1));
                if (state0Condition == null || state1Condition == null
                    || state0Condition.Value.parameter != state1Condition.Value.parameter) {
                    throw new DoNotOptimizeException("State conditions are not opposites");
                }
                var param = GetExistingParameter(state0Condition.Value.parameter);
                if (FullControllerBuilder.VRChatGlobalParams.Contains(param) && param.type == AnimatorControllerParameterType.Int) {
                    throw new DoNotOptimizeException("Uses an int VRC built-in, which means >1 is likely");
                }
            }

            var condition = BlendtreeMath.False();
            foreach (var transition in to0) {
                var all = BlendtreeMath.True();
                foreach (var c in transition) {
                    var input = GetExistingParameter(c.parameter);
                    var test = c.mode switch {
                        AnimatorConditionMode.If => BlendtreeMath.Equals(input, 0).Not(),
                        AnimatorConditionMode.IfNot => BlendtreeMath.Equals(input, 0),
                        AnimatorConditionMode.Greater => BlendtreeMath.GreaterThan(input, c.threshold),
                        AnimatorConditionMode.Less => BlendtreeMath.LessThan(input, c.threshold),
                        AnimatorConditionMode.Equals => BlendtreeMath.Equals(input, c.threshold),
                        AnimatorConditionMode.NotEqual => BlendtreeMath.Equals(input, c.threshold).Not(),
                        _ => throw new DoNotOptimizeException("Unknown condition type")
                    };
                    all = all.And(test);
                }
                condition = condition.Or(all);
            }
            var motion0 = Make0LengthClipForState(layer, state0);
            var motion1 = Make0LengthClipForState(layer, state1);
            var valid0 = validateBindingsService.HasValidBinding(motion0, avatarObject);
            var valid1 = validateBindingsService.HasValidBinding(motion1, avatarObject);
            if (!valid0 && !valid1) throw new DoNotOptimizeException("Contains no valid bindings");
            var candidate = condition.create(motion0, motion1);
            var trees = new AnimatorIterator.Trees();
            var addedTrees = trees.From(candidate).Count - trees.From(state0.motion).Count() - trees.From(state1.motion).Count();
            if (addedTrees > 6) throw new DoNotOptimizeException($"Optimized logic would add {addedTrees} trees, which is more expensive than a separate layer");
            directTree.Value.Add(candidate);
        }
        
        private static bool IsEntryOnlyState(VFLayer layer, VFState state) {
            return layer.defaultState == state && GetTransitionsTo(layer, state).Count == 0;
        }

        private static bool IsUnreachableState(VFLayer layer, VFState state) {
            return layer.defaultState != state && GetTransitionsTo(layer, state).Count == 0;
        }

        private static ICollection<VFTransitionBase> GetTransitionsTo(VFLayer layer, VFState state) {
            var output = new List<VFTransitionBase>();
            var ignoreTransitions = new HashSet<VFTransitionBase>();
            var entryState = layer.defaultState;

            if (layer.entryTransitions.Length == 1 &&
                layer.entryTransitions[0].conditions.Length == 0) {
                entryState = layer.entryTransitions[0].destinationState;
                ignoreTransitions.Add(layer.entryTransitions[0]);
            }

            foreach (var t in layer.allTransitions) {
                if (ignoreTransitions.Contains(t)) continue;
                if (t.destinationState == state || (t.isExit && entryState == state)) {
                    output.Add(t);
                }
            }
            return output.ToArray();
        }

        [CanBeNull]
        private VFMotion Make0LengthClipForState(VFLayer layer, VFState state) {
            if (state.motion == null) return null;

            if (state.motion.IsStatic()) {
                return state.motion.EvaluateMotion(1);
            }

            if (!state.motion.IsTwoState()) {
                throw new DoNotOptimizeException($"{state.name} contains a non-static clip with more than 2 keyframes");
            }

            var clipsInMotion = new AnimatorIterator.Clips().From(state.motion);
            if (clipsInMotion.Any(clip => clip.IsLooping())) {
                throw new DoNotOptimizeException($"{state.name} contains non-static motion that loops");
            }

            var hasNegativeTimeScaleClip = new AnimatorIterator.Trees().From(state.motion)
                .SelectMany(tree => tree.children)
                .Any(child => child.timeScale <= 0);
            if (hasNegativeTimeScaleClip) {
                throw new DoNotOptimizeException($"{state.name} contains a tree child with a timeScale <= 0");
            }

            var startMotion = state.motion.EvaluateMotion(0);
            var endMotion = state.motion.EvaluateMotion(1);

            if (state.timeParameterActive) {
                // TODO: This could also break if the animation tangents are not linear
                // TODO: We could detect if this is always 1 or always 0 and optimize it away
                if (string.IsNullOrWhiteSpace(state.timeParameter)) {
                    throw new DoNotOptimizeException($"{state.name} contains a motion time clip without a valid parameter");
                }
                var subTree = VFBlendTree1D.Create($"Layer {layer.name} - {state.name}", state.timeParameter);
                if (state.speed >= 0) {
                    subTree.Add(0, startMotion);
                    subTree.Add(1, endMotion);
                } else {
                    subTree.Add(0, endMotion);
                    subTree.Add(1, startMotion);
                }
                return subTree;
            } else if (state.motion is VFClip) {
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

        private ICollection<VFBinding> GetBindingsAnimatedInLayer(VFLayer layer) {
            return new AnimatorIterator.Clips().From(layer)
                .SelectMany(clip => clip.GetAllBindings())
                .Where(binding => validateBindingsService.IsValid(binding, avatarObject))
                .Select(binding => binding.Normalize(true))
                .ToImmutableHashSet();
        }

        private static AnimatorCondition? GetSingleCondition(IEnumerable<VFTransitionBase> transitions) {
            var allConditions = transitions
                .SelectMany(t => t.conditions)
                .Distinct()
                .ToList();
            if (allConditions.Count != 1) return null;
            return allConditions[0];
        }
    }
}
