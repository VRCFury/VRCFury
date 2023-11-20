using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils.Controller {
    public class VFController {
        private readonly AnimatorController ctrl;

        private VFController(AnimatorController ctrl) {
            this.ctrl = ctrl;
        }
    
        public static implicit operator VFController(AnimatorController d) => new VFController(d);
        public static implicit operator AnimatorController(VFController d) => d?.ctrl;
        public static implicit operator bool(VFController d) => d?.ctrl;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFController a && ctrl == a.ctrl)
                   || (other is AnimatorController b && ctrl == b)
                   || (other == null && ctrl == null);
        }
        public override int GetHashCode() => Tuple.Create(ctrl).GetHashCode();

        public VFLayer NewLayer(string name, int insertAt = -1) {
            // Unity breaks if name contains .
            name = name.Replace(".", "");

            ctrl.AddLayer(name);
            var layers = ctrl.layers;
            var layer = layers.Last();
            if (insertAt >= 0) {
                for (var i = layers.Length-1; i > insertAt; i--) {
                    layers[i] = layers[i - 1];
                }
                layers[insertAt] = layer;
            }
            layer.defaultWeight = 1;
            layer.stateMachine.anyStatePosition = VFState.MovePos(layer.stateMachine.entryPosition, 0, 1);
            ctrl.layers = layers;
            return new VFLayer(this, layer.stateMachine);
        }
    
        public void RemoveLayer(int i) {
            // Due to some unity bug, removing any layer from a controller
            // also removes ALL layers marked as synced for some reason.
            // VRChat synced layers are broken anyways, so we can just turn them off.
            ctrl.layers = ctrl.layers.Select(layer => {
                layer.syncedLayerIndex = -1;
                return layer;
            }).ToArray();
            ctrl.RemoveLayer(i);
        }

        public void RemoveParameter(int i) {
            ctrl.RemoveParameter(i);
        }

        public VFABool NewTrigger(string name) {
            return new VFABool(NewParam(name, AnimatorControllerParameterType.Trigger));
        }
        public VFABool NewBool(string name, bool def = false) {
            return new VFABool(NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def));
        }
        public VFAFloat NewFloat(string name, float def = 0) {
            return new VFAFloat(NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def));
        }
        public VFAInteger NewInt(string name, int def = 0) {
            return new VFAInteger(NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def));
        }
        public AnimatorControllerParameter NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
            var exists = GetParam(name);
            if (exists != null) {
                if (exists.type != type) {
                    throw new Exception(
                        $"VRCF tried to create parameter {name} with type {type} in controller {ctrl.name}," +
                        $" but parameter already exists with type {exists.type}");
                }
                return exists;
            }
            ctrl.AddParameter(name, type);
            var ps = ctrl.parameters;
            var param = ps[ps.Length-1];
            with?.Invoke(param);
            ctrl.parameters = ps;
            return param;
        }

        public AnimatorControllerParameter GetParam(string name) {
            return Array.Find(ctrl.parameters, other => other.name == name);
        }
    
        public IEnumerable<VFLayer> GetLayers() {
            return ctrl.layers.Select(l => new VFLayer(this, l.stateMachine));
        }

        public bool ContainsLayer(AnimatorStateMachine stateMachine) {
            return ctrl.layers.Any(l => l.stateMachine == stateMachine);
        }

        public int GetLayerId(AnimatorStateMachine stateMachine) {
            return ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == stateMachine)
                .Select(tuple => tuple.Item2)
                .First();
        }

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            var layers = ctrl.layers;
            if (index < 0 || index >= layers.Length) return null;
            return new VFLayer(this, layers[index].stateMachine);
        }

        public VFLayer GetLayer(AnimatorStateMachine stateMachine) {
            return GetLayer(GetLayerId(stateMachine));
        }

        public AnimatorControllerLayer[] layers {
            get => ctrl.layers;
            set => ctrl.layers = value;
        }

        public AnimatorControllerParameter[] parameters {
            get => ctrl.parameters;
            set => ctrl.parameters = value;
        }

        [CanBeNull]
        public static VFController CopyAndLoadController(RuntimeAnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            if (ctrl == null) {
                return null;
            }

            // Make a copy of everything
            ctrl = MutableManager.CopyRecursive(ctrl);

            // Collect any override controllers wrapping the main controller
            var overrides = new List<AnimatorOverrideController>();
            while (ctrl is AnimatorOverrideController ov) {
                overrides.Add(ov);
                ctrl = ov.runtimeAnimatorController;
            }

            // Bail if we hit a dead end
            if (!(ctrl is AnimatorController ac)) {
                return null;
            }
            
            // Apply override controllers
            if (overrides.Count > 0) {
                AnimatorIterator.ReplaceClips(ac, clip => {
                    return overrides
                        .Select(ov => ov[clip])
                        .Where(overrideClip => overrideClip != null)
                        .DefaultIfEmpty(clip)
                        .First();
                });
            }

            var output = new VFController(ac);
            output.FixNullStateMachines();
            output.FixLayer0Weight();
            output.ApplyBaseMask(type);
            return output;
        }

        /**
         * Some people have corrupt controller layers containing no state machine.
         * The simplest fix for this is for us to just stuff an empty state machine into it.
         * We can't just delete it because it would interfere with the layer index numbers.
         */
        private void FixNullStateMachines() {
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine == null) {
                    layer.stateMachine = new AnimatorStateMachine {
                        name = layer.name,
                        hideFlags = HideFlags.HideInHierarchy
                    };
                }
                return layer;
            }).ToArray();
        }

        /**
         * Layer 0 is always treated as fully weighted by unity, regardless of its actually setting.
         * This can do weird things when we move that layer around, so we always ensure that the
         * setting of the base layer is properly set to 1.
         */
        private void FixLayer0Weight() {
            var layer0 = GetLayer(0);
            if (layer0 == null) return;
            layer0.weight = 1;
        }

        /**
         * VRCF's handles masks by "applying" the base mask to every mask in the controller. This makes things like
         * merging controllers and features much easier. Later on, we recalculate a new base mask in FixMasksBuilder. 
         */
        private void ApplyBaseMask(VRCAvatarDescriptor.AnimLayerType type) {
            var isFx = type == VRCAvatarDescriptor.AnimLayerType.FX;
            var layer0 = GetLayer(0);
            if (layer0 == null) return;

            bool HasMuscles(VFLayer layer) {
                return new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetFloatBindings())
                    .Any(binding => binding.IsMuscle() || binding.IsProxyBinding());
            }

            var baseMask = layer0.mask;
            foreach (var layer in GetLayers()) {
                var useBaseMask = baseMask;
                if (isFx && useBaseMask == null && (HasMuscles(layer) || layer.mask != null)) {
                    useBaseMask = AvatarMaskExtensions.Empty();
                    useBaseMask.AllowAllTransforms();
                }
                if (layer.mask == useBaseMask) continue;
                if (layer.mask == null) {
                    layer.mask = AvatarMaskExtensions.Empty();
                    layer.mask.UnionWith(useBaseMask);
                } else {
                    layer.mask.IntersectWith(useBaseMask);
                }
            }
        }
    }
}
