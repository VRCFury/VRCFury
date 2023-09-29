using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void Apply() {
            var allControllers = manager.GetAllUsedControllers().ToArray();

            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    expectedMask = GetFxMask(c, allControllers);
                } else {
                    expectedMask = null;
                }

                var layer0 = ctrl.GetLayer(0);
                // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a
                // controller with no layers
                if (layer0 == null || layer0.mask != expectedMask) {
                    c.EnsureEmptyBaseLayer().mask = expectedMask;
                }
            }
        }

        /**
         * We build the gesture mask by unioning all the masks from the avatar base, plus any controllers
         * we've merged in. We also add left and right fingers, to ensure they're allowed so VRCFury-added
         * hand gestures can work.
         */
        private AvatarMask GetGestureMask(ControllerManager gesture) {
            var mask = AvatarMaskExtensions.Empty();
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            foreach (var union in gesture.GetUnionBaseMasks()) {
                if (union != null) {
                    mask.UnionWith(union);
                }
            }
            VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "gestureMask");
            return mask;
        }

        private AvatarMask GetFxMask(ControllerManager fx, IEnumerable<ControllerManager> allControllers) {
            var gestureContainsTransform = allControllers
                .Where(c => c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture)
                .SelectMany(c => c.GetClips())
                .SelectMany(clip => clip.GetAllBindings())
                .Any(binding => binding.type == typeof(Transform));
            if (!gestureContainsTransform) return null;

            foreach (var layer in fx.GetLayers()) {
                var oldMask = layer.mask;

                var transformedPaths = new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .ToImmutableHashSet();
                if (transformedPaths.Count == 0) continue;

                var mask = AvatarMaskExtensions.Empty();
                mask.SetTransforms(transformedPaths);
                if (oldMask != null) {
                    mask.IntersectWith(oldMask);
                }

                VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "fxMaskForLayer" + layer.GetLayerId());
                layer.mask = mask;
            }

            return null;
        }
    }
}
