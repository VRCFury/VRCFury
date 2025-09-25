using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    internal static class TpsConfigurer {
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
            VFGameObject bakeRoot,
            float worldLength
        ) {
            // Convert MeshRenderer to SkinnedMeshRenderer
            if (renderer is MeshRenderer) {
                var obj = renderer.owner();
                var staticMesh = renderer.GetMesh();
                var meshFilter = obj.GetComponent<MeshFilter>();
                var mats = renderer.sharedMaterials;
                var shadowCastingMode = renderer.shadowCastingMode;
                var receiveShadows = renderer.receiveShadows;
                var lightProbeUsage = renderer.lightProbeUsage;
                var reflectionProbeUsage = renderer.reflectionProbeUsage;
                var probeAnchor = renderer.probeAnchor;

                Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(meshFilter);

                var newSkin = obj.AddComponent<SkinnedMeshRenderer>();
                newSkin.SetMesh(staticMesh);
                newSkin.sharedMaterials = mats;
                newSkin.shadowCastingMode = shadowCastingMode;
                newSkin.receiveShadows = receiveShadows;
                newSkin.lightProbeUsage = lightProbeUsage;
                newSkin.reflectionProbeUsage = reflectionProbeUsage;
                newSkin.probeAnchor = probeAnchor;
                renderer = newSkin;
            }

            var skin = renderer as SkinnedMeshRenderer;
            if (skin == null) throw new VRCFBuilderException("Unknown renderer type");
            var mesh = skin.GetMesh();
            if (mesh == null) throw new Exception("Missing mesh");

            // Convert unweighted (static) meshes, to true skinned, rigged meshes
            if (mesh.boneWeights.Length == 0) {
                // This is put on this skin instead of in the bake root so that it doesn't get shown by headchop
                var mainBone = GameObjects.Create("SpsMainBone", skin.owner());
                var meshCopy = mesh.Clone("Needed to add a rig to make SPS compatible");
                meshCopy.boneWeights = meshCopy.vertices.Select(v => new BoneWeight { weight0 = 1 }).ToArray();
                meshCopy.bindposes = new[] {
                    Matrix4x4.identity, 
                };
                VRCFuryEditorUtils.MarkDirty(meshCopy);
                skin.bones = new Transform[] { mainBone };
                skin.SetMesh(meshCopy);
                mesh = meshCopy;
                VRCFuryEditorUtils.MarkDirty(skin);
            }

            skin.rootBone = bakeRoot;
            
            var bake = MeshBaker.BakeMesh(skin, skin.rootBone);
            var bounds = new Bounds();
            foreach (var vertex in bake.vertices) {
                bounds.Encapsulate(vertex);
            }

            var localLength = worldLength / bakeRoot.worldScale.z;
            bounds.Encapsulate(new Vector3(localLength * 2f,localLength * 2f,localLength * 2.5f));
            bounds.Encapsulate(new Vector3(localLength * -2f,localLength * -2f,localLength * 2.5f));
            bounds.Encapsulate(new Vector3(localLength * 2f,localLength * 2f,localLength * -0.5f));
            bounds.Encapsulate(new Vector3(localLength * -2f,localLength * -2f,localLength * -0.5f));
            skin.localBounds = bounds;
            skin.updateWhenOffscreen = false;

            if (EditorApplication.isPlaying) {
                skin.owner().AddComponent<VRCFuryNoUpdateWhenOffscreen>();
            }

            return skin;
        }

        public static void ConfigureTpsMaterial(
            SkinnedMeshRenderer skin,
            Material mat,
            float worldLength,
            float[] activeFromMask
        ) {
            var shaderRotation = Quaternion.identity;
            if (IsLocked(mat)) {
                throw new VRCFBuilderException(
                    "VRCFury SPS Plug has 'auto-configure TPS' checked, but material is locked. Please unlock the material using TPS to use this feature.");
            }
            if (DpsConfigurer.IsDps(mat)) {
                throw new VRCFBuilderException(
                    "VRCFury SPS Plug has 'auto-configure TPS' checked, but material has both TPS and Raliv DPS enabled in the Poiyomi settings. Disable DPS to continue.");
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
            mat.SetTexture(TpsBakedMesh, SpsBaker.Bake(skin, activeFromMask, true));
            VRCFuryEditorUtils.MarkDirty(mat);
        }
        
        private static Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);

        public static bool IsTps(Material mat) {
            if (mat == null) return false;
            return mat.HasProperty(TpsPenetratorEnabled) && mat.GetFloat(TpsPenetratorEnabled) > 0;
        }

        public static Quaternion GetTpsRotation(Material mat) {
            if (mat.HasProperty(TpsPenetratorForward)) {
                var c = mat.GetVector(TpsPenetratorForward);
                return Quaternion.LookRotation(new Vector3(c.x, c.y, c.z));
            }
            return Quaternion.identity;
        }

        public static bool IsLocked(Material mat) {
            return mat != null && mat.shader && mat.shader.name.ToLower().Contains("locked");
        }

        public static bool HasDpsOrTpsMaterial(Renderer r) {
            return r.sharedMaterials.Any(mat => DpsConfigurer.IsDps(mat) || IsTps(mat));
        }
    }
}
