using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal class VFControllerWithVrcType : VFController {
        public readonly VRCAvatarDescriptor.AnimLayerType vrcType;
        
        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return vrcType;
        }

        public VFControllerWithVrcType(AnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType vrcType) : base(ctrl) {
            this.vrcType = vrcType;
        }

        public static VFControllerWithVrcType CopyAndLoadController(RuntimeAnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            var baseCopy = VFController.CopyAndLoadController(ctrl);
            if (baseCopy == null) return null;
            var output = new VFControllerWithVrcType(baseCopy.GetRaw(), type);
            output.ApplyBaseMask(type);
            return output;
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
    }
}
