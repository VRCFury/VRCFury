using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class CleanupBaseMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupBaseMasks)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    var mask = new AvatarMask();

                    for (AvatarMaskBodyPart bodyPart = 0; bodyPart < AvatarMaskBodyPart.LastBodyPart; bodyPart++) {
                        //mask.SetHumanoidBodyPartActive(bodyPart, false);
                    }
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

                    foreach (var union in c.GetUnionBaseMasks()) {
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
                    expectedMask = mask;
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
    }
}
