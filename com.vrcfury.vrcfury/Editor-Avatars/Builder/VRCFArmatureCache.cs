using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Exceptions;
using VF.Injector;
using VF.Utils;

namespace VF.Builder {
    [VFService]
    internal class VRCFArmatureCache {
        private static readonly Dictionary<VFGameObject, VRCFArmatureCache> perFrame
            = new Dictionary<VFGameObject, VRCFArmatureCache>();
        private readonly Dictionary<HumanBodyBones, string> bonePaths = new Dictionary<HumanBodyBones, string>();
        private readonly Dictionary<HumanBodyBones, VFGameObject> boneObjects = new Dictionary<HumanBodyBones, VFGameObject>();
        private readonly HashSet<VFGameObject> nonEyeBoneParents = new HashSet<VFGameObject>();

        public static VRCFArmatureCache GetPerFrame(VFGameObject avatarObject) {
            return perFrame.GetOrCreate(
                avatarObject,
                () => new VRCFArmatureCache(avatarObject)
            );
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(perFrame.Clear, 0);
        }

        [VFAutowired]
        public VRCFArmatureCache(VFGameObject avatarObject) {
            if (avatarObject == null) return;
            var animator = avatarObject.GetComponent<Animator>();
            if (!animator || !animator.avatar) return;

            var so = new SerializedObject(animator.avatar);
            var skeletonIndexToBoneHash = GetSkeletonIndexToBoneHash(so);
            var boneHashToPath = GetBoneHashToPath(so);

            foreach (var bone in GetAllBoneTypes()) {
                var skeletonIndex = GetSkeletonIndex(so, bone);
                if (!skeletonIndexToBoneHash.TryGetValue(skeletonIndex, out var boneHash)) continue;
                if (!boneHashToPath.TryGetValue(boneHash, out var path)) continue;
                bonePaths[bone] = path;
                var obj = avatarObject.Find(path);
                if (obj != null) boneObjects[bone] = obj;
            }

            nonEyeBoneParents.Add(avatarObject);
            foreach (var pair in boneObjects) {
                if (pair.Key == HumanBodyBones.LeftEye || pair.Key == HumanBodyBones.RightEye) continue;
                var current = pair.Value;
                while (current != null && current != avatarObject) {
                    nonEyeBoneParents.Add(current);
                    current = current.parent;
                }
            }
        }

        public VFGameObject FindBoneOnArmatureOrNull(HumanBodyBones findBone) {
            try {
                return FindBoneOnArmatureOrException(findBone);
            } catch (Exception) {
                return null;
            }
        }

        public VFGameObject FindBoneOnArmatureOrException(HumanBodyBones findBone) {
            if (boneObjects.TryGetValue(findBone, out var obj)) {
                return obj;
            }
            if (bonePaths.TryGetValue(findBone, out var path)) {
                throw new VRCFBuilderException(
                    "Failed to find " + findBone + " object on avatar, but bone was listed in humanoid descriptor. " +
                    "Did you rename one of your avatar's bones on accident? The path to this bone should be:\n" +
                    path);
            }
            if (!bonePaths.ContainsKey(HumanBodyBones.Hips)) {
                throw new Exception($"{findBone} bone could not be found because avatar's rig is not set to humanoid");
            }
            throw new VRCFBuilderException($"{findBone} bone isn't set in this avatar's rig");
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

        private static ISet<HumanBodyBones> GetAllBoneTypes() {
            return VRCFEnumUtils.GetValues<HumanBodyBones>()
                .Where(bone => bone != HumanBodyBones.LastBone)
                .ToImmutableHashSet();
        }

        public IReadOnlyDictionary<HumanBodyBones, VFGameObject> GetAllBones() {
            return boneObjects;
        }

        public bool IsNonEyeBoneParent(VFGameObject obj) {
            return nonEyeBoneParents.Contains(obj);
        }
    }
}
