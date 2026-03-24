using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Hooks;

namespace VF.Builder {
    internal static class VRCFArmatureUtils {
        private class Cached {
            public Dictionary<HumanBodyBones, string> paths = new Dictionary<HumanBodyBones, string>();
            public Dictionary<HumanBodyBones, VFGameObject> objects = new Dictionary<HumanBodyBones, VFGameObject>();
        }

        private static ConditionalWeakTable<Transform, Cached> cache = new ConditionalWeakTable<Transform, Cached>();

        public static void ClearCache() {
            cache = new ConditionalWeakTable<Transform, Cached>();
        }

        public static VFGameObject FindBoneOnArmatureOrNull(VFGameObject avatarObject, HumanBodyBones findBone) {
            try {
                return FindBoneOnArmatureOrException(avatarObject, findBone);
            } catch (Exception) {
                return null;
            }
        }

        public static VFGameObject FindBoneOnArmatureOrException(VFGameObject avatarObject, HumanBodyBones findBone) {
            var data = Load(avatarObject);
            if (data.objects.TryGetValue(findBone, out var obj)) {
                return obj;
            }
            if (data.paths.TryGetValue(findBone, out var path)) {
                throw new VRCFBuilderException(
                    "Failed to find " + findBone + " object on avatar, but bone was listed in humanoid descriptor. " +
                    "Did you rename one of your avatar's bones on accident? The path to this bone should be:\n" +
                    path);
            }
            if (!data.paths.ContainsKey(HumanBodyBones.Hips)) {
                throw new Exception($"{findBone} bone could not be found because avatar's rig is not set to humanoid");
            }
            throw new VRCFBuilderException($"{findBone} bone isn't set in this avatar's rig");
        }

        public static void WarmupCache(VFGameObject avatarObject) {
            Load(avatarObject);
        }

        private static Cached Load(VFGameObject avatarObject) {
            if (cache.TryGetValue(avatarObject, out var cached)) {
                return cached;
            }
            cached = LoadUncached(avatarObject);
            cache.Add(avatarObject, cached);
            return cached;
        }

        private static Cached LoadUncached(VFGameObject avatarObject) {
            var output = new Cached();
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator) {
                return output;
            }
            if (!animator.avatar) {
                return output;
            }

            var so = new SerializedObject(animator.avatar);
            var skeletonIndexToBoneHash = GetSkeletonIndexToBoneHash(so);
            var boneHashToPath = GetBoneHashToPath(so);

            foreach (var bone in GetAllBones()) {
                var skeletonIndex = GetSkeletonIndex(so, bone);
                if (!skeletonIndexToBoneHash.TryGetValue(skeletonIndex, out var boneHash)) continue;
                if (!boneHashToPath.TryGetValue(boneHash, out var path)) continue;
                output.paths[bone] = path;
                var obj = VRCFObjectPathCache.Find(avatarObject, path);
                if (obj != null) output.objects[bone] = obj;
            }

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

        public static ISet<HumanBodyBones> GetAllBones() {
            return VRCFEnumUtils.GetValues<HumanBodyBones>()
                .Where(bone => bone != HumanBodyBones.LastBone)
                .ToImmutableHashSet();
        }

        public static IDictionary<HumanBodyBones, VFGameObject> GetAllBones(VFGameObject avatarObject) {
            var data = Load(avatarObject);
            return data.objects;
        }
    }
}
