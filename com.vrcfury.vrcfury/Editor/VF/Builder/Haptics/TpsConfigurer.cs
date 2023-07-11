using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature;
using VF.Inspector;
using VF.Model;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    public static class TpsConfigurer {
        private static readonly string TpsPenetratorKeyword = "TPS_Penetrator";
        private static readonly int TpsPenetratorEnabled = Shader.PropertyToID("_TPSPenetratorEnabled");
        public static readonly int TpsPenetratorLength = Shader.PropertyToID("_TPS_PenetratorLength");
        public static readonly int SpsLength = Shader.PropertyToID("_SPS_Length");
        public static readonly int SpsBakedLength = Shader.PropertyToID("_SPS_BakedLength");
        public static readonly int SpsBake = Shader.PropertyToID("_SPS_Bake");
        public static readonly int TpsPenetratorScale = Shader.PropertyToID("_TPS_PenetratorScale");
        private static readonly int TpsPenetratorRight = Shader.PropertyToID("_TPS_PenetratorRight");
        private static readonly int TpsPenetratorUp = Shader.PropertyToID("_TPS_PenetratorUp");
        public static readonly int TpsPenetratorForward = Shader.PropertyToID("_TPS_PenetratorForward");
        private static readonly int TpsIsSkinnedMeshRenderer = Shader.PropertyToID("_TPS_IsSkinnedMeshRenderer");
        private static readonly string TpsIsSkinnedMeshKeyword = "TPS_IsSkinnedMesh";
        private static readonly int TpsBakedMesh = Shader.PropertyToID("_TPS_BakedMesh");
        private static readonly int RalivPenetratorEnabled = Shader.PropertyToID("_PenetratorEnabled");

        public static SkinnedMeshRenderer ConfigureRenderer(
            Renderer renderer,
            Transform rootTransform,
            float worldLength,
            Texture2D mask,
            MutableManager mutableManager,
            bool useSps
        ) {
            if (useSps) {
                if (PlugSizeDetector.HasDpsMaterial(renderer)) {
                    throw new Exception(
                        $"VRCFury haptic plug was asked to configure SPS on renderer {renderer}," +
                        $" but it already has TPS or DPS. If you want to use SPS, use a regular shader" +
                        $" on the mesh instead.");
                }
            } else {
                if (!renderer.sharedMaterials.Any(m => IsTps(m))) {
                    return null;
                }
            }

            var canAutoRig = false;

            // Convert MeshRenderer to SkinnedMeshRenderer
            if (renderer is MeshRenderer) {
                var obj = renderer.gameObject;
                var meshFilter = obj.GetComponent<MeshFilter>();
                var mesh = meshFilter.sharedMesh;
                var mats = renderer.sharedMaterials;
                var anchor = renderer.probeAnchor;

                Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(meshFilter);

                var newSkin = obj.AddComponent<SkinnedMeshRenderer>();
                newSkin.sharedMesh = mesh;
                newSkin.sharedMaterials = mats;
                newSkin.probeAnchor = anchor;
                renderer = newSkin;
                canAutoRig = true;
            }

            var skin = renderer as SkinnedMeshRenderer;
            if (!skin) {
                throw new VRCFBuilderException("TPS material found on non-mesh renderer");
            }
            
            // Convert unweighted (static) meshes, to true skinned, rigged meshes
            if (skin.sharedMesh.boneWeights.Length == 0) {
                var mainBone = new GameObject("MainBone");
                mainBone.transform.SetParent(skin.transform, false);
                mainBone.transform.SetParent(rootTransform, true);
                var meshCopy = mutableManager.MakeMutable(skin.sharedMesh);
                meshCopy.boneWeights = meshCopy.vertices.Select(v => new BoneWeight { weight0 = 1 }).ToArray();
                meshCopy.bindposes = new[] {
                    Matrix4x4.identity, 
                };
                VRCFuryEditorUtils.MarkDirty(meshCopy);
                skin.bones = new[] { mainBone.transform };
                skin.sharedMesh = meshCopy;
                VRCFuryEditorUtils.MarkDirty(skin);
                canAutoRig = true;
            }
            
            skin.rootBone = rootTransform;

            if (canAutoRig && useSps) {
                AutoRig(skin, worldLength, mutableManager);
            }

            skin.sharedMaterials = skin.sharedMaterials
                .Select(mat => ConfigureMaterial(skin, mat, rootTransform, worldLength, mask, mutableManager, useSps))
                .ToArray();
            
            VRCFuryEditorUtils.MarkDirty(skin);

            var bake = MeshBaker.BakeMesh(skin, rootTransform);
            var bounds = new Bounds();
            foreach (var vertex in bake.vertices) {
                bounds.Encapsulate(vertex);
            }
            // This needs to be at least the distance of tooFar in the SPS shader, so that the lights are in range
            // before deformation may happen
            var multiplyLength = 2.5f;
            bounds.extents *= 2*multiplyLength;
            skin.localBounds = bounds;
            BoundingBoxFixBuilder.AdjustBoundingBox(skin);

            return skin;
        }

        private static void AutoRig(SkinnedMeshRenderer skin, float worldLength, MutableManager mutableManager) {
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
                bindPoses.Add(skin.rootBone.localToWorldMatrix * bone.worldToLocalMatrix);
            }

            if (skin.bones.Length != 1) {
                throw new Exception("Expected skin to contain exactly 1 main bone");
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
        
        public static Material ConfigureMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            Transform rootTransform,
            float worldLength,
            Texture2D mask,
            MutableManager mutableManager,
            bool useSps
        ) {
            if (useSps) {
                var m = mutableManager.MakeMutable(original);
                SpsPatcher.patch(m, mutableManager);
                m.SetFloat(SpsLength, worldLength);
                m.SetFloat(SpsBakedLength, worldLength);

                var bakedMesh2 = MeshBaker.BakeMesh(skin, rootTransform, true);
                if (bakedMesh2 == null)
                    throw new VRCFBuilderException("Failed to bake mesh for SPS configuration"); 
                var bake = SpsBaker.Bake(bakedMesh2, mutableManager.GetTmpDir());
                m.SetTexture(SpsBake, bake);
                return m;
            }
            
            var bakeUtil = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.BakeToVertexColors");
            if (bakeUtil == null) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but Poiyomi Pro TPS does not seem to be imported in project. (missing class)");
            }

            var meshInfoType = bakeUtil.GetNestedType("MeshInfo");
            var bakeMethod = bakeUtil.GetMethod(
                "BakePositionsToTexture", 
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { meshInfoType, typeof(Texture2D) },
                null
            );
            if (meshInfoType == null || bakeMethod == null) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but Poiyomi Pro TPS does not seem to be imported in project. (missing method)");
            }
            
            if (!IsTps(original)) return original;
            var mat = mutableManager.MakeMutable(original);
            
            var shaderRotation = Quaternion.identity;
            if (IsLocked(mat)) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but material is locked. Please unlock the material using TPS to use this feature.");
            }
            if (mat.HasProperty(RalivPenetratorEnabled) && mat.GetFloat(RalivPenetratorEnabled) > 0) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but material has both TPS and Raliv DPS enabled in the Poiyomi settings. Disable DPS to continue.");
            }

            var localScale = rootTransform.lossyScale;

            mat.EnableKeyword(TpsPenetratorKeyword);
            mat.SetFloat(TpsPenetratorEnabled, 1);
            mat.SetFloat(TpsPenetratorLength, worldLength);
            mat.SetVector(TpsPenetratorScale, ThreeToFour(localScale));
            mat.SetVector(TpsPenetratorRight, ThreeToFour(shaderRotation * Vector3.right));
            mat.SetVector(TpsPenetratorUp, ThreeToFour(shaderRotation * Vector3.up));
            mat.SetVector(TpsPenetratorForward, ThreeToFour(shaderRotation * Vector3.forward));
            mat.SetFloat(TpsIsSkinnedMeshRenderer, 1);
            mat.EnableKeyword(TpsIsSkinnedMeshKeyword);
            
            var meshInfo = Activator.CreateInstance(meshInfoType);
            var bakedMesh = MeshBaker.BakeMesh(skin, rootTransform);
            if (bakedMesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for TPS configuration"); 
            meshInfoType.GetField("bakedVertices").SetValue(meshInfo, bakedMesh.vertices);
            meshInfoType.GetField("bakedNormals").SetValue(meshInfo, bakedMesh.normals);
            meshInfoType.GetField("ownerRenderer").SetValue(meshInfo, skin);
            meshInfoType.GetField("sharedMesh").SetValue(meshInfo, skin.sharedMesh);
            Texture2D tex = null;
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                tex = (Texture2D)ReflectionUtils.CallWithOptionalParams(bakeMethod, null, meshInfo, mask);
            });
            if (string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(tex))) {
                throw new VRCFBuilderException("Failed to bake TPS texture");
            }
            mat.SetTexture(TpsBakedMesh, tex);
            VRCFuryEditorUtils.MarkDirty(mat);

            return mat;
        }
        
        private static Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);

        public static bool IsTps(Material mat) {
            return mat && mat.HasProperty(TpsPenetratorEnabled) && mat.GetFloat(TpsPenetratorEnabled) > 0;
        }
        
        public static bool IsSps(Material mat) {
            return mat && mat.HasProperty(SpsBake);
        }

        public static bool IsLocked(Material mat) {
            return mat.shader.name.ToLower().Contains("locked");
        }
    }
}
