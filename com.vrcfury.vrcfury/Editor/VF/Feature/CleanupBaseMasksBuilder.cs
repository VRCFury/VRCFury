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
            var allControllers = manager.GetAllUsedControllers();

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

        /**
         * We generate the FX mask by allowing all transforms, EXCEPT for those that are animated in Gesture and not in FX.
         * We disable those transforms so the animations will show through from Gesture. (Curiously, this isn't actually
         * needed if using WD on, because animations will always show through).
         */
        private AvatarMask GetFxMask(ControllerManager fx, IEnumerable<ControllerManager> allControllers) {
            var pathsInGesture = allControllers
                .Where(c => c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture)
                .SelectMany(GetAnimatedPaths)
                .Where(path => path != "")
                .ToImmutableHashSet();
            if (pathsInGesture.IsEmpty) return null;

            var pathsInFx = GetAnimatedPaths(fx);
            var mask = new AvatarMask();
            for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                mask.SetHumanoidBodyPartActive(bodyPart, false);
            }
            foreach (var path in pathsInFx) {
                mask.transformCount++;
                mask.SetTransformPath(mask.transformCount-1, path);
                mask.SetTransformActive(mask.transformCount-1, true);
            }
            VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "fxMask");
            return mask;
        }

        private IImmutableSet<string> GetAnimatedPaths(ControllerManager c) {
            return c.GetClips()
                .SelectMany(clip => clip.GetAllBindings())
                .Select(b => b.path)
                .ToImmutableHashSet();
        }
    }
}
