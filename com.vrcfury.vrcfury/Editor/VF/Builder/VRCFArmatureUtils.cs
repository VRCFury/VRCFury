using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder {
    public static class VRCFArmatureUtils {
        private static ConditionalWeakTable<Transform, Dictionary<HumanBodyBones, string>> cache
            = new ConditionalWeakTable<Transform, Dictionary<HumanBodyBones, string>>();

        public static void ClearCache() {
            cache = new ConditionalWeakTable<Transform, Dictionary<HumanBodyBones, string>>();
        }

        public static VFGameObject FindBoneOnArmatureOrNull(VFGameObject avatarObject, HumanBodyBones findBone) {
            try {
                return FindBoneOnArmatureOrException(avatarObject, findBone);
            } catch (Exception) {
                return null;
            }
        }

        public static VFGameObject FindBoneOnArmatureOrException(VFGameObject avatarObject, HumanBodyBones findBone) {
            var bonePath = FindBonePathOrException(avatarObject, findBone);

            var found = avatarObject.Find(bonePath);
            if (found == null) {
                throw new VRCFBuilderException(
                    "Failed to find " + findBone + " object on avatar, but bone was listed in humanoid descriptor. " +
                    "Did you rename one of your avatar's bones on accident? The path to this bone should be:\n" +
                    bonePath);
            }

            return found;
        }

        public static void WarmupCache(VFGameObject avatarObject) {
            Load(avatarObject);
        }
        
        private static string FindBonePathOrException(VFGameObject avatarObject, HumanBodyBones findBone) {
            var lookup = Load(avatarObject);

            if (!lookup.TryGetValue(findBone, out var path)) {
                throw new VRCFBuilderException(
                    "Bone isn't present in rig. Are you sure the rig for the avatar is humanoid and contains this bone?");
            }

            return path;
        }

        private static Dictionary<HumanBodyBones, string> Load(VFGameObject avatarObject) {
            if (cache.TryGetValue(avatarObject, out var cached)) {
                return cached;
            }

            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) {
                return new Dictionary<HumanBodyBones, string>();
            }
            if (!animator.avatar) {
                return new Dictionary<HumanBodyBones, string>();
            }

            var so = new SerializedObject(animator.avatar);
            var skeletonIndexToBoneHash = GetSkeletonIndexToBoneHash(so);
            var boneHashToPath = GetBoneHashToPath(so);
            var output = new Dictionary<HumanBodyBones, string>();
            foreach (var bone in GetAllBones()) {
                var skeletonIndex = GetSkeletonIndex(so, bone);
                if (!skeletonIndexToBoneHash.TryGetValue(skeletonIndex, out var boneHash)) continue;
                if (!boneHashToPath.TryGetValue(boneHash, out var path)) continue;
                output[bone] = path;
            }
            
            cache.Add(avatarObject, output);
            return output;
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
                return -1;
            }

            if (indexInBoneIndex < 0 || indexInBoneIndex >= array.arraySize) {
                return -1;
            }

            return array.GetArrayElementAtIndex(indexInBoneIndex).intValue;
        }

        private static Dictionary<int,long> GetSkeletonIndexToBoneHash(SerializedObject so) {
            var output = new Dictionary<int, long>();
            var boneHashArray = so.FindProperty("m_Avatar.m_Human.data.m_Skeleton.data.m_ID");
            if (boneHashArray == null || !boneHashArray.isArray) {
                throw new VRCFBuilderException("Bone hash array is missing");
            }
            for (int i = 0; i < boneHashArray.arraySize; i++) {
                output[i] = boneHashArray.GetArrayElementAtIndex(i).longValue;
            }
            return output;
        }

        private static Dictionary<long,string> GetBoneHashToPath(SerializedObject so) {
            var tosArray = so.FindProperty("m_TOS");
            if (tosArray == null || !tosArray.isArray) {
                throw new VRCFBuilderException("TOS array is missing");
            }
            var output = new Dictionary<long, string>();
            for (int i = 0; i < tosArray.arraySize; i++) {
                var element = tosArray.GetArrayElementAtIndex(i);
                if (element == null) continue;
                var hashProp = element.FindPropertyRelative("first");
                if (hashProp == null) continue;
                var pathProp = element.FindPropertyRelative("second");
                if (pathProp == null) continue;
                output[hashProp.longValue] = pathProp.stringValue;
            }
            return output;
        }

        public static IList<HumanBodyBones> GetAllBones() {
            return VRCFEnumUtils.GetValues<HumanBodyBones>()
                .Where(bone => bone != HumanBodyBones.LastBone)
                .ToArray();
        }

        public static IList<VFGameObject> GetAllBones(VFGameObject avatarObject) {
            return GetAllBones()
                .Select(bone => FindBoneOnArmatureOrNull(avatarObject, bone))
                .Where(bone => bone != null)
                .ToList();
        }
    }
}
