using UnityEngine;
using VF.Builder;

namespace com.vrcfury.api {
    /** Useful code utilities provided by VRCFury */
    public static class FuryUtils {
        /**
         * Finds the given bone transform from a GameObject containing an Animator with an attached rig.
         * An Exception will be thrown if the bone cannot be found.
         */
        public static Transform GetBone(Transform avatarObject, HumanBodyBones bone) {
            return VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, bone);
        }
    }
}
