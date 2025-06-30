using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Service;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils.Controller {
    internal class VFController {
        private readonly AnimatorController ctrl;

        public VFController(AnimatorController ctrl) {
            this.ctrl = ctrl;
        }
    
        //public static implicit operator VFController(AnimatorController d) => new VFController(d);
        //public static implicit operator AnimatorController(VFController d) => d?.ctrl;
        //public static implicit operator bool(VFController d) => d?.ctrl != null;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFController a && ctrl == a.ctrl)
                   || (other is AnimatorController b && ctrl == b)
                   || (other == null && ctrl == null);
        }
        public override int GetHashCode() => Tuple.Create(ctrl).GetHashCode();

        public AnimatorController GetRaw() {
            return ctrl;
        }
        
        protected virtual string NewLayerName(string name) {
            return name;
        }
        
        public virtual VFLayer NewLayer(string name, int insertAt = -1) {
            name = NewLayerName(name);

            // Unity breaks if name contains .
            name = name.Replace(".", "");

            ctrl.AddLayer(name);
            var sm = ctrl.layers.Last().stateMachine;
            var layer = new VFLayer(ctrl, sm);
            if (insertAt >= 0) {
                layer.Move(insertAt);
            }
            layer.weight = 1;
            sm.anyStatePosition = VFState.CalculateOffsetPosition(sm.entryPosition, 0, 1);
            VrcfObjectFactory.Register(sm);
            return layer;
        }
        
        /**
         * BEWARE: This consumes the ENTIRE asset file containing "other"
         * The animator controller (and its sub-assets) should be owned by vrcfury, and should
         * be the ONLY THING in that file!!!
         */
        public void TakeOwnershipOf(VFController other, bool putOnTop = false, bool prefix = true) {
            // Merge Layers
            if (prefix) {
                foreach (var layer in other.layers) {
                    layer.name = NewLayerName(layer.name);
                }
            }

            if (putOnTop) {
                ctrl.layers = other.ctrl.layers.Concat(ctrl.layers).ToArray();
            } else {
                ctrl.layers = ctrl.layers.Concat(other.ctrl.layers).ToArray();
            }

            other.ctrl.layers = new AnimatorControllerLayer[] { };
            
            // Merge Params
            foreach (var p in other.parameters) {
                _NewParam(p.name, p.type, n => {
                    n.defaultBool = p.defaultBool;
                    n.defaultFloat = p.defaultFloat;
                    n.defaultInt = p.defaultInt;
                });
            }

            other.parameters = new AnimatorControllerParameter[] { };
        }

        public void RemoveParameter(int i) {
            ctrl.RemoveParameter(i);
        }

        public VFABool _NewBool(string name, bool def = false) {
            var p = _NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def);
            return new VFABool(p.name, p.defaultBool);
        }
        public VFAFloat _NewFloat(string name, float def = 0) {
            var p = _NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def);
            return new VFAFloat(p.name, p.defaultFloat);
        }
        public VFAInteger _NewInt(string name, int def = 0) {
            var p = _NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def);
            return new VFAInteger(p.name, p.defaultInt);
        }
        public AnimatorControllerParameter _NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
            var exists = GetParam(name);
            if (exists != null) {
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
    
        public IList<VFLayer> GetLayers() {
            return ctrl.layers.Select(l => new VFLayer(ctrl, l.stateMachine)).ToArray();
        }
        public IList<VFLayer> layers => GetLayers();

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            var ls = ctrl.layers;
            if (index < 0 || index >= ls.Length) return null;
            return new VFLayer(ctrl, ls[index].stateMachine);
        }

        public AnimatorControllerParameter[] parameters {
            get => ctrl.parameters;
            set => ctrl.parameters = value;
        }

        [CanBeNull]
        public static VFControllerWithVrcType CopyAndLoadController(RuntimeAnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            if (ctrl == null) {
                return null;
            }

            // Make a copy of everything
            ctrl = ctrl.Clone(addPrefix: $"Copied from {ctrl.name}/");

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
            
            var output = new VFControllerWithVrcType(ac, type);
            output.RemoveInvalidParameters();
            output.FixNullStateMachines();
            output.FixBadTransitions();
            output.RemoveBadBehaviours();
            output.ReplaceSyncedLayers();
            output.RemoveDuplicateStateMachines();

            // Apply override controllers
            if (overrides.Count > 0) {
                output.ReplaceClips(clip => {
                    return overrides
                        .Select(ov => ov[clip])
                        .Where(overrideClip => overrideClip != null)
                        .DefaultIfEmpty(clip)
                        .First();
                });
            }

            // Make sure all masks are unique, so we don't modify one and affect another
            foreach (var layer in output.GetLayers()) {
                if (layer.mask != null) {
                    layer.mask = layer.mask.Clone();
                }
            }
            
            output.FixLayer0Weight();
            output.ApplyBaseMask(type);
            NoBadControllerParamsService.RemoveWrongParamTypes(output);
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
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
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
            var layer0 = GetLayer(0);
            if (layer0 == null) return;

            var baseMask = layer0.mask;
            if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                if (baseMask == null) {
                    baseMask = AvatarMaskExtensions.DefaultFxMask();
                } else {
                    baseMask = baseMask.Clone();
                }
            } else if (type == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                if (baseMask == null) {
                    // Technically, we should throw here. The VRCSDK will complain and prevent the user from uploading
                    // until they fix this. But we fix it here for them temporarily so they can use play mode for now.
                    // Gesture controllers merged using Full Controller with no base mask will slip through and be allowed
                    // by this.
                    baseMask = AvatarMaskExtensions.Empty();
                    baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                    baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                } else {
                    baseMask = baseMask.Clone();
                    // If the base mask is just one hand, assume that they put in controller with just a left and right hand layer,
                    // and meant to have both in the base mask.
                    if (baseMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers))
                        baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                    if (baseMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers))
                        baseMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                }
            } else {
                // VRChat does not use the base mask on any other controller types
                return;
            }

            // Because of some unity bug, ONLY the muscle part of the base mask is actually applied to the child layers
            // The transform part of the base mask DOES NOT impact lower layers!!
            baseMask.AllowAllTransforms();

            foreach (var layer in GetLayers()) {
                if (layer.mask == null) {
                    layer.mask = baseMask.Clone();
                } else {
                    layer.mask.IntersectWith(baseMask);
                }
            }
        }

        private void RemoveBadBehaviours() {
            foreach (var layer in layers) {
                layer.RemoveBadBehaviours();
            }
        }
        
        private void FixBadTransitions() {
            foreach (var layer in layers) {
                // layer.RewriteTransitions will automatically fix if the transitions array is null
                layer.RewriteTransitions(t => {
                    // Remove any transitions that are null
                    if (t == null) return null;
                    // Fix conditions arrays that are null
                    if (t.conditions == null) {
                        t.conditions = new AnimatorCondition[] { };
                    }
                    // Remove any transitions that are missing required conditions
                    if (t is AnimatorStateTransition st && !st.hasExitTime && !t.conditions.Any()) {
                        return null;
                    }
                    return t;
                });
            }
        }

        /**
         * Synced layers are... "very" broken in unity and in vrchat. In unity, if you delete a layer higher than the synced
         * layer, the synced layer is randomly deleted. In vrchat, synced layers don't really work properly at all.
         * When dealing with write defaults issues, synced layers break everything because the motions in the synced layer may need
         * a different WD setting than the states in the other layer.
         * We can "sorta" work around this issue by just replacing the synced layer with an identical copy. This means that timing sync won't work,
         * but it's better than nothing.
         */
        private void ReplaceSyncedLayers() {
            ctrl.layers = ctrl.layers.Select((layer, id) => {
                if (layer.syncedLayerIndex < 0 || layer.syncedLayerIndex == id) {
                    layer.syncedLayerIndex = -1;
                    return layer;
                }
                if (layer.syncedLayerIndex >= ctrl.layers.Length) {
                    var sm = VrcfObjectFactory.Create<AnimatorStateMachine>();
                    sm.name = layer.name;
                    layer.stateMachine = sm;
                    layer.syncedLayerIndex = -1;
                    return layer;
                }

                var copy = ctrl.layers[layer.syncedLayerIndex].stateMachine.Clone();
                layer.syncedLayerIndex = -1;
                layer.stateMachine = copy;
                foreach (var state in new AnimatorIterator.States().From(new VFLayer(ctrl, layer.stateMachine))) {
                    var originalState = state.GetCloneSource();
                    state.motion = layer.GetOverrideMotion(originalState);
                    state.behaviours = layer.GetOverrideBehaviours(originalState);
                    layer.SetOverrideMotion(originalState, null);
                    layer.SetOverrideBehaviours(originalState, Array.Empty<StateMachineBehaviour>());
                }

                return layer;
            }).ToArray();

        }

        /**
         * Some systems (modular avatar) can improperly add multiple layers with the same state machine.
         * This wrecks havoc, as making changes to one of the layers can impact both, while typically there is
         * expected to be no cross-talk. Since there's basically no legitimate reason for the same state machine
         * to be used more than once in the same controller, we can just nuke the copies.
         */
        private void RemoveDuplicateStateMachines() {
            var seenStateMachines = new HashSet<AnimatorStateMachine>();
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine != null) {
                    if (seenStateMachines.Contains(layer.stateMachine)) {
                        return null;
                    }
                    seenStateMachines.Add(layer.stateMachine);
                }
                return layer;
            }).NotNull().ToArray();
        }
        
        /**
         * Some tools add parameters with an invalid type (not bool, trigger, float, int, etc)
         * This causes the VRCSDK to blow up and break the mirror clone and throw exceptions in console.
         * https://feedback.vrchat.com/bug-reports/p/invalid-parameter-type-within-a-controller-breaks-mirror-clone-and-spams-output
         */
        private void RemoveInvalidParameters() {
            ctrl.parameters = ctrl.parameters.Where(p => VRCFEnumUtils.IsValid(p.type)).ToArray();
        }

        public void RewriteParameters(Func<string, string> rewriteParamNameNullUnsafe, bool includeWrites = true, ICollection<VFLayer> limitToLayers = null) {
            string RewriteParamName(string str) {
                if (string.IsNullOrEmpty(str)) return str;
                return rewriteParamNameNullUnsafe(str);
            }
            var affectsLayers = GetLayers()
                .Where(l => limitToLayers == null || limitToLayers.Contains(l))
                .ToArray();
            
            // Params
            if (includeWrites && limitToLayers == null) {
                var prms = ctrl.parameters;
                foreach (var p in prms) {
                    p.name = RewriteParamName(p.name);
                }

                ctrl.parameters = prms;
                VRCFuryEditorUtils.MarkDirty(ctrl);
            }

            // States
            foreach (var state in new AnimatorIterator.States().From(affectsLayers)) {
                if (state.speedParameterActive) {
                    state.speedParameter = RewriteParamName(state.speedParameter);
                }
                if (state.cycleOffsetParameterActive) {
                    state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                }
                if (state.mirrorParameterActive) {
                    state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                }
                if (state.timeParameterActive) {
                    state.timeParameter = RewriteParamName(state.timeParameter);
                }
                VRCFuryEditorUtils.MarkDirty(state);
            }
            
            foreach (var b in affectsLayers.SelectMany(layer => layer.allBehaviours)) {
                // VRCAvatarParameterDriver
                if (includeWrites && b is VRCAvatarParameterDriver oldB) {
                    foreach (var p in oldB.parameters) {
                        p.name = RewriteParamName(p.name);
#if VRCSDK_HAS_DRIVER_COPY
                        p.source = RewriteParamName(p.source);
#endif
                    }
                    VRCFuryEditorUtils.MarkDirty(b);
                }

                // VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                if (b is VRCAnimatorPlayAudio audio) {
                    audio.ParameterName = RewriteParamName(audio.ParameterName);
                }
#endif
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in new AnimatorIterator.Clips().From(affectsLayers)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.GetPropType() != EditorCurveBindingType.Aap) return binding;
                        binding.propertyName = RewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in new AnimatorIterator.Trees().From(affectsLayers)) {
                tree.RewriteParameters(RewriteParamName);
            }

            // Transitions
            foreach (var layer in affectsLayers) {
                layer.RewriteConditions(cond => {
                    cond.parameter = RewriteParamName(cond.parameter);
                    return cond;
                });
            }
        }

        public VFController Clone() {
            return new VFController(ctrl.Clone());
        }
        
        public void ReplaceClips(Func<AnimationClip, AnimationClip> replace) {
            Motion RewriteMotion(Motion motion) {
                if (motion is AnimationClip clip) {
                    return replace(clip);
                }
                if (motion is BlendTree tree) {
                    tree.RewriteChildren(child => {
                        child.motion = RewriteMotion(child.motion);
                        return child;
                    });
                    return tree;
                }
                return motion;
            }
            
            foreach (var state in layers.SelectMany(l => l.allStates)) {
                state.motion = RewriteMotion(state.motion);
            }
        }
    }
}
