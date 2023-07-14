using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder.Haptics {
    public static class SpsAutoRigger {
        public static void AutoRig(SkinnedMeshRenderer skin, float worldLength, MutableManager mutableManager) {
            if (skin.bones.Length != 1) {
                return;
            }

            var mesh = skin.sharedMesh;
            mesh = mutableManager.MakeMutable(mesh);
            skin.sharedMesh = mesh;

            var bake = MeshBaker.BakeMesh(skin, skin.rootBone);
            var boneCount = 10;
            var lastParent = skin.rootBone;
            var bones = new List<Transform>();
            var bindPoses = new List<Matrix4x4>();
            var localLength = worldLength / skin.rootBone.lossyScale.z;
            for (var i = 0; i < boneCount; i++) {
                var bone = new GameObject("VrcFuryAutoRig" + i).transform;
                bone.SetParent(lastParent, false);
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
                closestBoneId = Math.Max(0, Math.Min(boneCount + boneOffset - 1, closestBoneId));
                otherBoneId = Math.Max(0, Math.Min(boneCount + boneOffset - 1, otherBoneId));

                weights[i] = CalculateWeight(closestBoneId, otherBoneId, distanceToOther);
            }

            var physbone = bones.First().gameObject.AddComponent<VRCPhysBone>();
            physbone.integrationType = VRCPhysBoneBase.IntegrationType.Advanced;
            physbone.pull = 0.8f;
            physbone.spring = 0.1f;
            physbone.stiffness = 0.3f;

            mesh.boneWeights = weights;
        }

        private static BoneWeight CalculateWeight(int closestBoneId, int otherBoneId, float distanceToOther) {
            var overlap = 0.5f;
            if (distanceToOther > overlap) {
                return new BoneWeight() {
                    weight0 = 1,
                    boneIndex0 = closestBoneId,
                };
            } else {
                var weightOfOther = (1 - (distanceToOther / overlap)) * 0.5f;
                return new BoneWeight() {
                    weight0 = 1-weightOfOther,
                    boneIndex0 = closestBoneId,
                    weight1 = weightOfOther,
                    boneIndex1 = otherBoneId
                };
            }
        }
    }
}