using System;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature;
using VF.Inspector;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    public static class TpsConfigurer {
        private static readonly string TpsPenetratorKeyword = "TPS_Penetrator";
        private static readonly int TpsPenetratorEnabled = Shader.PropertyToID("_TPSPenetratorEnabled");
        private static readonly int TpsPenetratorLength = Shader.PropertyToID("_TPS_PenetratorLength");
        private static readonly int TpsPenetratorScale = Shader.PropertyToID("_TPS_PenetratorScale");
        private static readonly int TpsPenetratorRight = Shader.PropertyToID("_TPS_PenetratorRight");
        private static readonly int TpsPenetratorUp = Shader.PropertyToID("_TPS_PenetratorUp");
        private static readonly int TpsPenetratorForward = Shader.PropertyToID("_TPS_PenetratorForward");
        private static readonly int TpsIsSkinnedMeshRenderer = Shader.PropertyToID("_TPS_IsSkinnedMeshRenderer");
        private static readonly string TpsIsSkinnedMeshKeyword = "TPS_IsSkinnedMesh";
        private static readonly int TpsBakedMesh = Shader.PropertyToID("_TPS_BakedMesh");

        // Converts MeshRenderers or 0-bone SkinnedMeshRenderers to real weighted SkinnedMeshRenderers
        public static SkinnedMeshRenderer NormalizeRenderer(
            Renderer renderer,
            Transform rootTransform,
            MutableManager mutableManager,
            float worldLength
        ) {
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
                var mainBone = GameObjects.Create("MainBone", rootTransform, useTransformFrom: skin.transform);
                var meshCopy = mutableManager.MakeMutable(skin.sharedMesh);
                meshCopy.boneWeights = meshCopy.vertices.Select(v => new BoneWeight { weight0 = 1 }).ToArray();
                meshCopy.bindposes = new[] {
                    Matrix4x4.identity, 
                };
                VRCFuryEditorUtils.MarkDirty(meshCopy);
                skin.bones = new Transform[] { mainBone };
                skin.sharedMesh = meshCopy;
                VRCFuryEditorUtils.MarkDirty(skin);
            }

            skin.rootBone = rootTransform;
            
            var bake = MeshBaker.BakeMesh(skin, skin.rootBone);
            var bounds = new Bounds();
            foreach (var vertex in bake.vertices) {
                bounds.Encapsulate(vertex);
            }

            var localLength = worldLength / rootTransform.lossyScale.z;
            bounds.Encapsulate(new Vector3(localLength * 1.5f,localLength * 1.5f,localLength * 2.5f));
            bounds.Encapsulate(new Vector3(localLength * -1.5f,localLength * -1.5f,localLength * 2.5f));
            skin.localBounds = bounds;

            return skin;
        }

        public static Material ConfigureTpsMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            float worldLength,
            float[] activeFromMask,
            MutableManager mutableManager
        ) {
            var mat = mutableManager.MakeMutable(original);
            
            var shaderRotation = Quaternion.identity;
            if (IsLocked(mat)) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but material is locked. Please unlock the material using TPS to use this feature.");
            }
            if (DpsConfigurer.IsDps(original)) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but material has both TPS and Raliv DPS enabled in the Poiyomi settings. Disable DPS to continue.");
            }

            var localScale = skin.rootBone.lossyScale;

            mat.EnableKeyword(TpsPenetratorKeyword);
            mat.SetFloat(TpsPenetratorEnabled, 1);
            mat.SetFloat(TpsPenetratorLength, worldLength);
            mat.SetVector(TpsPenetratorScale, ThreeToFour(localScale));
            mat.SetVector(TpsPenetratorRight, ThreeToFour(shaderRotation * Vector3.right));
            mat.SetVector(TpsPenetratorUp, ThreeToFour(shaderRotation * Vector3.up));
            mat.SetVector(TpsPenetratorForward, ThreeToFour(shaderRotation * Vector3.forward));
            mat.SetFloat(TpsIsSkinnedMeshRenderer, 1);
            mat.EnableKeyword(TpsIsSkinnedMeshKeyword);
            mat.SetTexture(TpsBakedMesh, SpsBaker.Bake(skin, mutableManager.GetTmpDir(), activeFromMask, true));
            VRCFuryEditorUtils.MarkDirty(mat);

            return mat;
        }
        
        private static Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);

        public static bool IsTps(Material mat) {
            return mat && mat.HasProperty(TpsPenetratorEnabled) && mat.GetFloat(TpsPenetratorEnabled) > 0;
        }

        public static Quaternion GetTpsRotation(Material mat) {
            if (mat.HasProperty(TpsPenetratorForward)) {
                var c = mat.GetVector(TpsPenetratorForward);
                return Quaternion.LookRotation(new Vector3(c.x, c.y, c.z));
            }
            return Quaternion.identity;
        }

        public static bool IsLocked(Material mat) {
            return mat && mat.shader && mat.shader.name.ToLower().Contains("locked");
        }

        public static bool HasDpsOrTpsMaterial(Renderer r) {
            return r.sharedMaterials.Any(mat => DpsConfigurer.IsDps(mat) || IsTps(mat));
        }
    }
}
