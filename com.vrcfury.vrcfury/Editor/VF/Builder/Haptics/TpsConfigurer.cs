using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature;
using VF.Inspector;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    public static class TpsConfigurer {
        private static readonly string TpsPenetratorKeyword = "TPS_Penetrator";
        private static readonly int TpsPenetratorEnabled = Shader.PropertyToID("_TPSPenetratorEnabled");
        public static readonly int TpsPenetratorLength = Shader.PropertyToID("_TPS_PenetratorLength");
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
            MutableManager mutableManager
        ) {
            if (!renderer.sharedMaterials.Any(m => IsTps(m))) {
                return null;
            }
            
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
            }

            var skin = renderer as SkinnedMeshRenderer;
            if (!skin) {
                throw new VRCFBuilderException("TPS material found on non-mesh renderer");
            }
            
            // Convert unweighted (static) meshes, to true skinned, rigged meshes
            if (skin.sharedMesh.boneWeights.Length == 0) {
                var mainBone = new GameObject("MainBone");
                mainBone.transform.SetParent(rootTransform, false);
                var meshCopy = mutableManager.MakeMutable(skin.sharedMesh);
                meshCopy.boneWeights = meshCopy.vertices.Select(v => new BoneWeight { weight0 = 1 }).ToArray();
                meshCopy.bindposes = new[] {
                    Matrix4x4.identity, 
                };
                VRCFuryEditorUtils.MarkDirty(meshCopy);
                skin.bones = new[] { mainBone.transform };
                skin.sharedMesh = meshCopy;
                VRCFuryEditorUtils.MarkDirty(skin);
            }

            skin.sharedMaterials = skin.sharedMaterials
                .Select(mat => ConfigureMaterial(skin, mat, rootTransform, worldLength, mask, mutableManager))
                .ToArray();

            skin.rootBone = rootTransform;
            VRCFuryEditorUtils.MarkDirty(skin);
            
            BoundingBoxFixBuilder.AdjustBoundingBox(skin);

            return skin;
        }
        
        public static Material ConfigureMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            Transform rootTransform,
            float worldLength,
            Texture2D mask,
            MutableManager mutableManager
        ) {
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

        public static bool IsLocked(Material mat) {
            return mat.shader.name.ToLower().Contains("locked");
        }
    }
}
