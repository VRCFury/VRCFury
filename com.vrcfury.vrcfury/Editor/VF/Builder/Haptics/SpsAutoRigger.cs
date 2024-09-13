using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder.Haptics {
    internal static class SpsAutoRigger {
        public static void AutoRig(SkinnedMeshRenderer skin, VFGameObject bakeRoot, float worldLength, float worldRadius, float[] activeFromMask) {
            float GetActive(int i) {
                return activeFromMask == null ? 1 : activeFromMask[i];
            }

            if (skin.bones.Length != 1) {
                return;
            }

            var mesh = skin.GetMutableMesh("Needed to add rig for SPS auto-rig");
            if (mesh == null) throw new Exception("Missing mesh");
            var bake = MeshBaker.BakeMesh(skin, skin.rootBone);
            const int boneCount = 10;

            // This is left outside of the bake root so that it isn't shown by head chop
            var autoRigRoot = GameObjects.Create("SpsAutoRig", skin.owner(), useTransformFrom: bakeRoot);

            var lastParent = autoRigRoot;
            var bones = new List<Transform>();
            var bindPoses = new List<Matrix4x4>();
            var localLength = worldLength / skin.rootBone.lossyScale.z;
            var localRadius = worldRadius / skin.rootBone.lossyScale.z;
            for (var i = 0; i < boneCount; i++) {
                var bone = GameObjects.Create("Bone" + i, lastParent);
                var pos = bone.localPosition;
                pos.z = localLength / boneCount;
                bone.localPosition = pos;
                bones.Add(bone);
                lastParent = bone;
                bindPoses.Add(bone.worldToLocalMatrix * skin.localToWorldMatrix);
            }

            var boneOffset = skin.bones.Length;
            skin.bones = skin.bones.Concat(bones).ToArray();
            mesh.bindposes = mesh.bindposes.Concat(bindPoses).ToArray();
            var weights = mesh.boneWeights;
            for (var i = 0; i < mesh.vertices.Length; i++) {
                var bakedVert = bake.vertices[i];
                if (bakedVert.z < 0) continue;
                var boneNum = bakedVert.z / localLength * boneCount;

                var closestBoneId = (int)boneNum + boneOffset;
                var otherBoneId = (boneNum % 0.5) > 0.5 ? closestBoneId + 1 : closestBoneId - 1;
                var distanceToOther = (boneNum % 0.5) > 0.5 ? (1 - boneNum % 1) : boneNum % 1;
                closestBoneId = VrcfMath.Clamp(closestBoneId, 0, boneCount + boneOffset - 1);
                otherBoneId = VrcfMath.Clamp(otherBoneId, 0, boneCount + boneOffset - 1);

                weights[i] = CalculateWeight(closestBoneId, otherBoneId, distanceToOther, GetActive(i));
            }
            mesh.boneWeights = weights;

            var physbone = autoRigRoot.AddComponent<VRCPhysBone>();
            physbone.integrationType = VRCPhysBoneBase.IntegrationType.Advanced;
            physbone.pull = 0.8f;
            physbone.spring = 0.1f;
            physbone.stiffness = 0.3f;
            physbone.rootTransform = bones.First();

            var radiusEnd = Mathf.Max(0.0f, 1.0f - localRadius / localLength);
            physbone.radiusCurve = AnimationCurve.Linear(radiusEnd, 1.0f, 1.0f, 0.0f);
            physbone.radius = localRadius;
        }

        private static BoneWeight CalculateWeight(int closestBoneId, int otherBoneId, float distanceToOther, float activeFromMask) {
            var overlap = 0.5f;
            if (distanceToOther > overlap) {
                return new BoneWeight() {
                    weight0 = activeFromMask, boneIndex0 = closestBoneId, // closest bone
                    weight1 = 1 - activeFromMask, boneIndex1 = 0, // root bone
                };
            } else {
                var weightOfOther = (1 - (distanceToOther / overlap)) * 0.5f;
                return new BoneWeight() {
                    weight0 = (1-weightOfOther) * activeFromMask, boneIndex0 = closestBoneId, // closest bone
                    weight1 = weightOfOther * activeFromMask, boneIndex1 = otherBoneId, // other bone
                    weight2 = 1 - activeFromMask, boneIndex2 = 0, // root bone
                };
            }
        }
    }
}