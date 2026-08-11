using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class FinalizeAvatarDescriptorService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;

        [FeatureBuilderAction(FeatureOrder.FinalizeAvatarDescriptor)]
        public void Apply() {
#if ! VRC_NEW_PUBLIC_SDK
            // Old VRCSDK didn't apply these fixes itself after running postprocessors
            ApplyFixes();
#else
            // When the VRCSDK isn't involved, we have to run the fixes ourselves
            if (!IsActuallyUploadingHook.Get()) {
                ApplyFixes();
            }
#endif
        }

        private void ApplyFixes() {
            // The VRCSDK usually builds the debug window name lookup before the avatar is built, so we have
            // to update it with our newly-added states
            RebuildDebugHashes(avatar);

            // The VRCSDK usually does this before the avatar is built in VRCAvatarDescriptorEditor3AnimLayerInit
            var layers = avatar.baseAnimationLayers;
            for (var i = 0; i < layers.Length; i++) {
                var layer = layers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.Gesture || layer.type == VRCAvatarDescriptor.AnimLayerType.FX) {
                    var c = layer.animatorController as AnimatorController;
                    if (c != null && c.layers.Length > 0) {
                        layer.mask = c.layers[0].avatarMask;
                        layers[i] = layer;
                    }
                }
            }
        }

        /**
         * VRC calculates the animator debug map before vrcfury is invoked, so if we want our states to show up in the
         * debug panel, we need to add them to the map ourselves.
         */
        private static void RebuildDebugHashes(VRCAvatarDescriptor avatar) {
            foreach (var found in VRCAvatarUtils.GetAllControllers(avatar)) {
                if (found.isDefault) continue;
                var ac = found.controller as AnimatorController;
                if (ac == null) continue;

                foreach (var layer in ac.layers) {
                    var rootStateMachine = layer.stateMachine;
                    ProcessStateMachine(rootStateMachine, "");
                    void ProcessStateMachine(AnimatorStateMachine stateMachine, string prefix) {
                        //Update prefix
                        prefix = prefix + stateMachine.name + ".";

                        //States
                        foreach (var state in stateMachine.states) {
                            var hash = new VRCAvatarDescriptor.DebugHash();
                            var fullName = prefix + state.state.name;
                            hash.hash = Animator.StringToHash(fullName);
                            hash.name = fullName.Remove(0, rootStateMachine.name.Length + 1);
                            avatar.animationHashSet.Add(hash);
                        }

                        foreach (var subMachine in stateMachine.stateMachines)
                            ProcessStateMachine(subMachine.stateMachine, prefix);
                    }
                }
            }
            avatar.Dirty();
        }
    }
}
