using UnityEngine;
using VF.Model;
using VF.Model.Feature;

namespace com.vrcfury.api.Components {
    /** Create an instance using <see cref="FuryComponents"/> */
    public class FuryArmatureLink {
        private readonly ArmatureLink c;

        internal FuryArmatureLink(GameObject obj) {
            var vf = obj.AddComponent<VRCFury>();
            c = new ArmatureLink();
            c.propBone = obj;
            c.linkTo.Clear();
            vf.content = c;
        }

        /** Optional. Defaults to the object you created the component on */
        public void LinkFrom(GameObject obj) {
            c.propBone = obj;
        }

        public void LinkTo(HumanBodyBones bone, string offset = "") {
            c.linkTo.Add(new ArmatureLink.LinkTo() {
                bone = bone,
                offset = offset
            });
        }
        
        public void LinkTo(GameObject obj, string offset = "") {
            c.linkTo.Add(new ArmatureLink.LinkTo() {
                useBone = false,
                useObj = true,
                obj = obj,
                offset = offset
            });
        }
        
        public void LinkTo(string path) {
            c.linkTo.Add(new ArmatureLink.LinkTo() {
                useBone = false,
                useObj = false,
                offset = path
            });
        }

        /**
         * Determines if the component should also merge children with matching names.
         * 
         * Optional. Defaults to "auto" mode, which is recursive only if a skin uses one of the
         * merged objects as bones.
         */
        public void SetRecursive(bool recursive) {
            c.linkMode = recursive
                ? ArmatureLink.ArmatureLinkMode.SkinRewrite
                : ArmatureLink.ArmatureLinkMode.ReparentRoot;
        }
        
        /**
         * If true, the linked object will be moved to match the position, rotation, and scale of the target object.
         * If false, the linked object will not be moved during the link process.
         *
         * Optional. Defaults to "auto" mode, which will align only if Recursive is true.
         */
        public void SetAlign(bool align) {
            c.keepBoneOffsets2 = align
                ? ArmatureLink.KeepBoneOffsets.No
                : ArmatureLink.KeepBoneOffsets.Yes;
        }
    }
}
