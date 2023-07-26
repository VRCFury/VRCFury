using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class CleanupBaseMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupBaseMasks)]
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

                if (ctrl.layers.Length == 0) {
                    // don't need to worry about masks when there are no layers
                } else if (c.GetMask(0) == expectedMask) {
                    // base mask is already good
                } else {
                    c.EnsureEmptyBaseLayer();
                    c.SetMask(0, expectedMask);
                }
            }
        }

        /**
         * We build the gesture mask by unioning all the masks from the avatar base, plus any controllers
         * we've merged in. We also add left and right fingers, to ensure they're allowed so VRCFury-added
         * hand gestures can work.
         */
        private AvatarMask GetGestureMask(ControllerManager gesture) {
            var mask = new AvatarMask();

            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                mask.SetHumanoidBodyPartActive(bodyPart, false);
            }
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            foreach (var union in gesture.GetUnionBaseMasks()) {
                if (union == null) {
                    continue;
                }
                for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                    if (union.GetHumanoidBodyPartActive(bodyPart))
                        mask.SetHumanoidBodyPartActive(bodyPart, true);
                }
                for (var i = 0; i < union.transformCount; i++) {
                    if (union.GetTransformActive(i)) {
                        mask.transformCount++;
                        mask.SetTransformPath(mask.transformCount-1, union.GetTransformPath(i));
                        mask.SetTransformActive(mask.transformCount-1, true);
                    }
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
                var layerId = fx.GetLayerId(layer);
                var oldMask = fx.GetMask(layerId);

                var transformedPaths = new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .ToImmutableHashSet();
                if (transformedPaths.Count == 0) continue;
                
                var mask = new AvatarMask();
                for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                    mask.SetHumanoidBodyPartActive(bodyPart, false);
                }
                mask.SetTransforms(transformedPaths);
                mask.IntersectWith(oldMask);
                VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "fxMaskForLayer" + layerId);
                fx.SetMask(layerId, mask);
            }

            return null;
        }
    }
}
