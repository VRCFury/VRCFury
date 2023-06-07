using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder {
    public class VRCFArmatureUtils {
        private static FieldInfo parentNameField = 
            typeof(SkeletonBone).GetField("parentName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public static GameObject FindBoneOnArmatureOrNull(GameObject avatarObject, HumanBodyBones findBone) {
            try {
                return FindBoneOnArmatureOrException(avatarObject, findBone);
            } catch (Exception e) {
                Debug.LogWarning("Failed to find bone " + findBone + ": " + e.Message);
                return null;
            }
        }

        /**
         * This basically does what Animator.GetBoneTransform SHOULD do, except GetBoneTransform randomly sometimes
         * returns bones on clothing armatures instead of the avatar, and also sometimes returns null for no reason.
         */
        public static GameObject FindBoneOnArmatureOrException(GameObject avatarObject, HumanBodyBones findBone) {
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) {
                throw new VRCFBuilderException("Avatar does not contain an Animator. Are you sure the avatar's rig is set to Humanoid?");
            }
            if (!animator.avatar) {
                throw new VRCFBuilderException("Avatar's Animator does not have a rig present. Are you sure the avatar's rig is set to Humanoid?");
            }

            var so = new SerializedObject(animator.avatar);
            var skeletonIndex = GetSkeletonIndex(so, findBone);
            var boneHash = GetBoneHashFromSkeletonIndex(so, skeletonIndex);
            var bonePath = GetBonePathFromBoneHash(so, boneHash);

            var found = avatarObject.transform.Find(bonePath);
            if (!found) {
                throw new VRCFBuilderException(
                    "Failed to find " + findBone + " object on avatar, but bone was listed in humanoid descriptor. " +
                    "Did you rename one of your avatar's bones on accident? The path to this bone should be:\n" +
                    bonePath);
            }
            return found.gameObject;
        }

        private static int GetSkeletonIndex(SerializedObject so, HumanBodyBones humanoidIndex) {
            int indexInBoneIndex;
            string boneIndexProp;

            if (humanoidIndex <= HumanBodyBones.Chest) {
                indexInBoneIndex = (int)humanoidIndex;
                boneIndexProp = "m_Avatar.m_Human.data.m_HumanBoneIndex";
            } else if (humanoidIndex <= HumanBodyBones.Jaw) {
                indexInBoneIndex = (int)humanoidIndex + 1;
                boneIndexProp = "m_Avatar.m_Human.data.m_HumanBoneIndex";
            } else if (humanoidIndex == HumanBodyBones.UpperChest) {
                indexInBoneIndex = 9;
                boneIndexProp = "m_Avatar.m_Human.data.m_HumanBoneIndex";
            } else if (humanoidIndex <= HumanBodyBones.LeftLittleDistal) {
                indexInBoneIndex = (int)humanoidIndex - (int)HumanBodyBones.LeftThumbProximal;
                boneIndexProp = "m_Avatar.m_Human.data.m_LeftHand.data.m_HandBoneIndex";
            } else if (humanoidIndex <= HumanBodyBones.RightLittleDistal) {
                indexInBoneIndex = (int)humanoidIndex - (int)HumanBodyBones.RightThumbProximal;
                boneIndexProp = "m_Avatar.m_Human.data.m_RightHand.data.m_HandBoneIndex";
            } else {
                throw new VRCFBuilderException("Unknown bone index " + humanoidIndex);
            }

            var array = so.FindProperty(boneIndexProp);
            if (array == null || !array.isArray) {
                throw new VRCFBuilderException("Missing humanoid bone index array: " + boneIndexProp);
            }

            if (indexInBoneIndex < 0 || indexInBoneIndex >= array.arraySize) {
                throw new VRCFBuilderException("Missing humanoid bone index array element: " + boneIndexProp + " " + indexInBoneIndex);
            }

            var skeletonIndex = array.GetArrayElementAtIndex(indexInBoneIndex).intValue;
            if (skeletonIndex < 0) {
                throw new VRCFBuilderException(
                    "Bone isn't present in rig. Are you sure the rig for the avatar is humanoid and contains this bone?");
            }
            return skeletonIndex;
        }

        private static long GetBoneHashFromSkeletonIndex(SerializedObject so, int skeletonIndex) {
            var boneHashArray = so.FindProperty("m_Avatar.m_Human.data.m_Skeleton.data.m_ID");
            if (boneHashArray == null || !boneHashArray.isArray) {
                throw new VRCFBuilderException("Bone hash array is missing");
            }

            if (skeletonIndex < 0 || skeletonIndex >= boneHashArray.arraySize) {
                throw new VRCFBuilderException("Bone hash array is missing element: " + skeletonIndex);
            }

            return boneHashArray.GetArrayElementAtIndex(skeletonIndex).longValue;
        }
        
        private static string GetBonePathFromBoneHash(SerializedObject so, long boneHash) {
            var tosArray = so.FindProperty("m_TOS");
            for (int i = 0; i < tosArray.arraySize; i++) {
                var element = tosArray.GetArrayElementAtIndex(i);
                if (element == null) continue;
                var hashProp = element.FindPropertyRelative("first");
                if (hashProp == null) continue;
                if (boneHash != hashProp.longValue) continue;
                var pathProp = element.FindPropertyRelative("second");
                if (pathProp == null) continue;
                return pathProp.stringValue;
            }

            throw new VRCFBuilderException("Missing bone hash from TOS array: " + boneHash);
        }
    }
}
