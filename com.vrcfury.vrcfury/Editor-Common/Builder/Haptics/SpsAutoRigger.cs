using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    internal static class SpsAutoRigger {
        public static Renderer AutoRig(Renderer renderer, VFGameObject bakeRoot, float worldLength, float worldRadius, float[] activeFromMask) {
            if (renderer is SkinnedMeshRenderer existingSkin && existingSkin.bones.Length > 1) return renderer;

            SkinnedMeshRenderer skin;
            if (renderer is MeshRenderer mr) {
                var obj = mr.owner();
                var staticMesh = mr.GetMesh();
                var meshFilter = obj.GetComponent<MeshFilter>();
                var mats = mr.sharedMaterials;
                var shadowCastingMode = mr.shadowCastingMode;
                var receiveShadows = mr.receiveShadows;
                var lightProbeUsage = mr.lightProbeUsage;
                var reflectionProbeUsage = mr.reflectionProbeUsage;
                var probeAnchor = mr.probeAnchor;

                Object.DestroyImmediate(mr);
                Object.DestroyImmediate(meshFilter);

                skin = obj.AddComponent<SkinnedMeshRenderer>();
                skin.SetMesh(staticMesh);
                skin.sharedMaterials = mats;
                skin.shadowCastingMode = shadowCastingMode;
                skin.receiveShadows = receiveShadows;
                skin.lightProbeUsage = lightProbeUsage;
                skin.reflectionProbeUsage = reflectionProbeUsage;
                skin.probeAnchor = probeAnchor;
            } else if (renderer is SkinnedMeshRenderer s) {
                skin = s;
            } else {
                return renderer;
            }

            var mesh = skin.GetMutableMesh("SPS Autorig");
            if (mesh == null) throw new Exception("Missing mesh");

            if (skin.bones.Length == 0 || mesh.boneWeights.Length == 0) {
                var mainBone = GameObjects.Create("SpsMainBone", skin.owner());
                mesh.boneWeights = mesh.vertices.Select(v => new BoneWeight { weight0 = 1 }).ToArray();
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.Dirty();
                skin.bones = new Transform[] { mainBone };
            }

            float GetActive(int i) {
                return activeFromMask == null ? 1 : activeFromMask[i];
            }

            var bake = MeshBaker.BakeMesh(skin, bakeRoot);
            const int boneCount = 10;

            // This is left outside of the bake root so that it isn't shown by head chop
            var autoRigRoot = GameObjects.Create("SpsAutoRig", skin.owner(), useTransformFrom: bakeRoot);

            var lastParent = autoRigRoot;
            var bones = new List<Transform>();
            var bindPoses = new List<Matrix4x4>();
            var localLength = worldLength / bakeRoot.worldScale.z;
            var localRadius = worldRadius / bakeRoot.worldScale.z;
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
            mesh.RecalculateBounds();

            var physbone = autoRigRoot.AddComponent<VRCPhysBone>();
            physbone.integrationType = VRCPhysBoneBase.IntegrationType.Advanced;
            physbone.pull = 0.8f;
            physbone.spring = 0.1f;
            physbone.stiffness = 0.3f;
            physbone.rootTransform = bones.First();

            var radiusEnd = Mathf.Max(0.0f, 1.0f - localRadius / localLength);
            physbone.radiusCurve = AnimationCurve.Linear(radiusEnd, 1.0f, 1.0f, 0.0f);
            physbone.radius = localRadius;

            return skin;
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
