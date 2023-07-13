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
        private static readonly int TpsPenetratorLength = Shader.PropertyToID("_TPS_PenetratorLength");
        private static readonly int TpsPenetratorScale = Shader.PropertyToID("_TPS_PenetratorScale");
        private static readonly int TpsPenetratorRight = Shader.PropertyToID("_TPS_PenetratorRight");
        private static readonly int TpsPenetratorUp = Shader.PropertyToID("_TPS_PenetratorUp");
        private static readonly int TpsPenetratorForward = Shader.PropertyToID("_TPS_PenetratorForward");
        private static readonly int TpsIsSkinnedMeshRenderer = Shader.PropertyToID("_TPS_IsSkinnedMeshRenderer");
        private static readonly string TpsIsSkinnedMeshKeyword = "TPS_IsSkinnedMesh";
        private static readonly int TpsBakedMesh = Shader.PropertyToID("_TPS_BakedMesh");

        public static void ConfigureRenderer(
            Renderer renderer,
            Transform rootTransform,
            float worldLength,
            Texture2D mask,
            MutableManager mutableManager,
            bool useSps
        ) {
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
                SpsAutoRigger.AutoRig(skin, worldLength, mutableManager);
            }

            var configuredTps = false;
            skin.sharedMaterials = skin.sharedMaterials
                .Select(mat => {
                    if (useSps) {
                        return SpsConfigurer.ConfigureSpsMaterial(skin, mat, worldLength, mask, mutableManager);
                    } else if (IsTps(mat)) {
                        configuredTps = true;
                        return ConfigureTpsMaterial(skin, mat, worldLength, mask, mutableManager);
                    }
                    return mat;
                })
                .ToArray();

            if (!useSps && !configuredTps) {
                throw new VRCFBuilderException(
                    "VRCFury Haptic Plug has 'auto-configure TPS' checked, but no material on the linked renderer has TPS enabled in the Poiyomi settings.");
            }
            
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
        }

        private static Material ConfigureTpsMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            float worldLength,
            Texture2D mask,
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
            mat.SetTexture(TpsBakedMesh, SpsBaker.Bake(skin, mutableManager.GetTmpDir(), mask, true));
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
