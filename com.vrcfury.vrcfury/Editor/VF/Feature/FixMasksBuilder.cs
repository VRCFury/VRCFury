using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void Apply() {
            FixGestureConflict();

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
                // controller with no layers
                if (layer0 == null || layer0.mask != expectedMask) {
                    c.EnsureEmptyBaseLayer().mask = expectedMask;
                }
            }
        }

        private void FixGestureConflict() {
            if (manager.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }

            var gesture = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
            var gestureContainsTransform = gesture.GetClips()
                .SelectMany(clip => clip.GetAllBindings())
                .Any(binding => binding.type == typeof(Transform));
            if (!gestureContainsTransform) return;

            var fx = manager.GetFx();
            fx.TakeOwnershipOf(gesture, putOnTop: true);
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

        private AvatarMask GetFxMask(ControllerManager fx) {
            var mask = AvatarMaskExtensions.Empty();
            mask.AllowAllTransforms();
            foreach (var layer in fx.GetLayers()) {
                if (layer.mask != null) {
                    mask.UnionWith(layer.mask);
                }
            }

            if (mask.AllowsAnyMuscles()) {
                return mask;
            }
            return null;
        }
    }
}
