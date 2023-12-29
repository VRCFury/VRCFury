using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [VFService]
    public class FixMasksBuilder : FeatureBuilder {
        private readonly HashSet<AnimatorStateMachine> migratedFromGesture = new HashSet<AnimatorStateMachine>();

        public bool IsMigratedFromGesture(AnimatorStateMachine sm) {
            return migratedFromGesture.Contains(sm);
        }

        [FeatureBuilderAction(FeatureOrder.FixGestureFxConflict)]
        public void FixGestureFxConflict() {
            if (manager.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }
            var gesture = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);

            var gestureContainsTransform = gesture.GetClips()
                .SelectMany(clip => clip.GetAllBindings())
                .Any(binding => binding.type == typeof(Transform));

            var activateGestureToFxTransfer = gestureContainsTransform || DoesFxControlHands();
            if (!activateGestureToFxTransfer) {
                return;
            }

            migratedFromGesture.UnionWith(gesture.GetRaw().GetLayers().Select(l => l.stateMachine));

            var fx = manager.GetFx();
            fx.TakeOwnershipOf(gesture.GetRaw(), putOnTop: true);
        }
        
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void FixMasks() {
            foreach (var layer in GetFx().GetLayers()) {
                // For any layers we added to FX without masks, give them the default FX mask
                if (layer.mask == null) {
                    layer.mask = AvatarMaskExtensions.DefaultFxMask();
                }
                
                // Remove redundant FX masks if they're not needed
                if (layer.mask.AllowsAllTransforms() && !layer.HasMuscles()) {
                    layer.mask = null;
                }
            }

            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    expectedMask = GetFxMask(c);
                }

                var layer0 = ctrl.GetLayer(0);
                // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a
                //   controller with no layers
                // On FX, ALWAYS make an empty base layer, because for some reason transition times can break
                //   and animate immediately when performed within the base layer
                if (layer0 == null || layer0.mask != expectedMask || c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    c.EnsureEmptyBaseLayer().mask = expectedMask;
                }
            }
        }

        /**
         * We build the gesture base mask by unioning all the masks from the other layers.
         */
        private AvatarMask GetGestureMask(ControllerManager gesture) {
            var mask = AvatarMaskExtensions.Empty();
            foreach (var layer in gesture.GetLayers()) {
                if (layer.mask == null) throw new Exception("Gesture layer unexpectedly contains no mask");
                mask.UnionWith(layer.mask);
            }
            return mask;
        }

        private bool DoesFxControlHands() {
            return manager.GetFx().GetLayers()
                .Any(layer => layer.mask != null &&
                              (layer.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers)
                               || layer.mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers)));
        }

        /**
         * If a project uses WD off, and animates ANY muscle within a controller, that controller "claims ownership"
         * of every muscle allowed by its mask. This means that it's very important that we only allow FX to
         * have as few muscles as possible, because animating hands within FX would bust the entire rest of the avatar
         * if the mask allowed it.
         */
        private AvatarMask GetFxMask(ControllerManager fx) {
            if (DoesFxControlHands()) {
                var mask = AvatarMaskExtensions.DefaultFxMask();
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                return mask;
            }

            return null;
        }
    }
}
