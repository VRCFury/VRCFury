using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder.Haptics {
    internal static class TpsConfigurer {
        private static readonly string TpsPenetratorKeyword = "TPS_Penetrator";
        private static readonly string TpsPenetratorEnabledName = "_TPSPenetratorEnabled";
        private static readonly string TpsPenetratorLengthName = "_TPS_PenetratorLength";
        private static readonly string TpsPenetratorScaleName = "_TPS_PenetratorScale";
        private static readonly string TpsPenetratorRightName = "_TPS_PenetratorRight";
        private static readonly string TpsPenetratorUpName = "_TPS_PenetratorUp";
        private static readonly string TpsPenetratorForwardName = "_TPS_PenetratorForward";
        private static readonly string TpsIsSkinnedMeshRendererName = "_TPS_IsSkinnedMeshRenderer";
        private static readonly string TpsBakedMeshName = "_TPS_BakedMesh";
        private static readonly string TpsIsSkinnedMeshKeyword = "TPS_IsSkinnedMesh";

        public static void ConfigureTpsMaterial(
            Renderer skin,
            Transform origin,
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

            var localScale = origin.lossyScale;

            mat.EnableKeyword(TpsPenetratorKeyword);
            mat.SetFloatFast(TpsPenetratorEnabledName, 1);
            mat.SetFloatFast(TpsPenetratorLengthName, worldLength);
            mat.SetVectorFast(TpsPenetratorScaleName, ThreeToFour(localScale));
            mat.SetVectorFast(TpsPenetratorRightName, ThreeToFour(shaderRotation * Vector3.right));
            mat.SetVectorFast(TpsPenetratorUpName, ThreeToFour(shaderRotation * Vector3.up));
            mat.SetVectorFast(TpsPenetratorForwardName, ThreeToFour(shaderRotation * Vector3.forward));
            mat.SetFloatFast(TpsIsSkinnedMeshRendererName, 1);
            mat.EnableKeyword(TpsIsSkinnedMeshKeyword);
            mat.SetTextureFast(TpsBakedMeshName, SpsBaker.Bake(skin, origin, activeFromMask, true));
            mat.Dirty();
        }
        
        private static Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);

        public static bool IsTps(Material mat) {
            if (mat == null) return false;
            var shader = mat.shader;
            if (shader == null) return false;
            return mat.GetPropertyType(TpsPenetratorEnabledName) != null &&
                   mat.TryGetFloatFast(TpsPenetratorEnabledName, out var enabled) &&
                   enabled > 0;
        }

        public static Quaternion GetTpsRotation(Material mat) {
            if (mat.GetPropertyType(TpsPenetratorForwardName) != null) {
                mat.TryGetVectorFast(TpsPenetratorForwardName, out var c);
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
