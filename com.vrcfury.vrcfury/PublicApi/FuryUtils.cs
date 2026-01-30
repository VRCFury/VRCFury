using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;

namespace com.vrcfury.api {
    /** Useful code utilities provided by VRCFury */
    [PublicAPI]
    public static class FuryUtils {
        /**
         * Finds the given bone transform from a GameObject containing an Animator with an attached rig.
         * An Exception will be thrown if the bone cannot be found.
         */
        public static GameObject GetBone(GameObject avatarObject, HumanBodyBones bone) {
            return VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, bone);
        }
        
        /// <summary>
        /// Returns the parented target objects and the string offset, null if an armatureLink isn't present on object.
        /// If GameObject is null, then HumanBodyBones is the option selected.
        /// </summary>
        /// <returns>(targetObject, target skeleton bone, offset) </returns>
        public static List<(GameObject, HumanBodyBones, string)> GetArmatureLinkTargets(GameObject obj) {
            // Attempt to get the VRCFury component from the GameObject
            var vf = obj.GetComponent<VRCFury>();
            if (vf == null) {
                Debug.LogError("VRCFury component not found on this GameObject.");
                return null;
            }
    
            // Cast the content to ArmatureLink
            ArmatureLink armatureLink = vf.content as ArmatureLink;
            if (armatureLink == null) {
                Debug.LogError("Object does not contain an instance of ArmatureLink.");
                return null;
            }

            return armatureLink.GetLinkTargets();
        }
    }
}
