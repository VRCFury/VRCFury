using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils.Controller {
    internal class VFControllerWithVrcType : VFController {
        public readonly VRCAvatarDescriptor.AnimLayerType vrcType;
        
        public new VRCAvatarDescriptor.AnimLayerType GetType() {
            return vrcType;
        }

        public VFControllerWithVrcType(AnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType vrcType) : base(ctrl) {
            this.vrcType = vrcType;
        }
    }
}
