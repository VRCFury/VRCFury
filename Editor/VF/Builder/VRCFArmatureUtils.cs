using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VF.Builder {
    public class VRCFArmatureUtils {
        private static FieldInfo parentNameField = 
            typeof(SkeletonBone).GetField("parentName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        /**
         * This basically does what animator.GetBoneTransform SHOULD do, except GetBoneTransform randomly sometimes
         * returns bones on clothing armatures instead of the avatar, and also sometimes returns null for no reason.
         */
        public static GameObject FindBoneOnArmature(GameObject avatarObject, HumanBodyBones findBone) {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator || !animator.avatar) {
                throw new VRCFBuilderException(
                    "ArmatureLink found no humanoid animator on avatar.");
            }

            var humanDescription = animator.avatar.humanDescription;
            var humanBoneName = Enum.GetName(typeof(HumanBodyBones), findBone);
            var avatarBoneName = humanDescription.human
                .FirstOrDefault(humanBone => humanBone.humanName == humanBoneName)
                .boneName;

            // Unity tries to find the root bone BY NAME, which often might be the wrong one. It might even be the
            // one in the prop. So we need to find it ourself with better logic.
            var skeleton = humanDescription.skeleton;
            bool DoesBoneMatch(GameObject obj, SkeletonBone bone) {
                if (bone.name != obj.name) return false;
                if (obj.transform.parent.gameObject != avatarObject) {
                    var boneParentName = (string)parentNameField.GetValue(bone);
                    if (boneParentName != obj.transform.parent.name) return false;
                }
                return true;
            }
            bool IsProbablyInSkeleton(GameObject obj) {
                if (obj == null) return false;
                if (obj == avatarObject) return true;
                if (!skeleton.Any(b => DoesBoneMatch(obj, b))) return false;
                return IsProbablyInSkeleton(obj.transform.parent.gameObject);
            }
            var eligibleAvatarBones = avatarObject.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == avatarBoneName)
                .Select(t => t.gameObject)
                .Where(IsProbablyInSkeleton)
                .ToList();
            if (eligibleAvatarBones.Count == 0) {
                return null;
            }
            if (eligibleAvatarBones.Count > 1) {
                throw new VRCFBuilderException(
                    "ArmatureLink found multiple possible matching " + findBone + " bones on avatar.");
            }
            return eligibleAvatarBones[0];
        }
    }
}