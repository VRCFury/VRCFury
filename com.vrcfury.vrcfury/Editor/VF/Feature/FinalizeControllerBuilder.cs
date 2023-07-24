using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FinalizeControllerBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeController)]
        public void Apply() {
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            // The VRCSDK usually builds the debug window name lookup before the avatar is built, so we have
            // to update it with our newly-added states
            foreach (var c in manager.GetAllUsedControllers()) {
                RebuildDebugHashes(avatar, c);
            }
            
            // The VRCSDK usually does this before the avatar is built in VRCAvatarDescriptorEditor3AnimLayerInit
            var layers = avatar.baseAnimationLayers;
            for (var i = 0; i < layers.Length; i++) {
                var layer = layers[i];
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.Gesture || layer.type == VRCAvatarDescriptor.AnimLayerType.FX) {
                    var c = layer.animatorController as AnimatorController;
                    if (c && c.layers.Length > 0) {
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
        private static void RebuildDebugHashes(VRCAvatarDescriptor avatar, ControllerManager ctrl) {
            foreach (var layer in ctrl.GetManagedLayers()) {
                ProcessStateMachine(layer, "");
                void ProcessStateMachine(AnimatorStateMachine stateMachine, string prefix) {
                    //Update prefix
                    prefix = prefix + stateMachine.name + ".";

                    //States
                    foreach (var state in stateMachine.states) {
                        VRCAvatarDescriptor.DebugHash hash = new VRCAvatarDescriptor.DebugHash();
                        string fullName = prefix + state.state.name;
                        hash.hash = Animator.StringToHash(fullName);
                        hash.name = fullName.Remove(0, layer.name.Length + 1);
                        avatar.animationHashSet.Add(hash);
                    }

                    foreach (var subMachine in stateMachine.stateMachines)
                        ProcessStateMachine(subMachine.stateMachine, prefix);
                }
            }
            VRCFuryEditorUtils.MarkDirty(avatar);
        }
    }
}
