using System;
using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class DpsUtils {
        public enum LegacyDpsLightType {
            None,
            Hole,
            Ring,
            Front,
            Tip
        }
        public static LegacyDpsLightType GetLegacyDpsLightType(Light light) {
            if (light.range >= 0.5) return LegacyDpsLightType.None; // Outside of range
            var secondDecimal = (int)Math.Round((light.range % 0.1) * 100);
            if ((light.color.maxColorComponent > 1 && light.color.a > 0)) return LegacyDpsLightType.None; // For some reason, dps tip lights are (1,1,1,255)
            if (secondDecimal == 9 || secondDecimal == 8) {
                // Stock DPS tips use RGB bytes (1, 1, 1). Normal visible lights are not markers.
                return light.type == LightType.Point
                    && (light.color.a == 0 || light.color.a > 1 || light.color.maxColorComponent <= 1f / 255)
                    ? LegacyDpsLightType.Tip : LegacyDpsLightType.None;
            }
            if ((light.color.maxColorComponent > 0 && light.color.a > 0)) return LegacyDpsLightType.None; // Visible light
            if (secondDecimal == 1 || secondDecimal == 3) return LegacyDpsLightType.Hole;
            if (secondDecimal == 2 || secondDecimal == 4) return LegacyDpsLightType.Ring;
            if (secondDecimal == 5 || secondDecimal == 6) return LegacyDpsLightType.Front;
            return LegacyDpsLightType.None;
        }

        private const string RalivPenetratorEnabledName = "_PenetratorEnabled";
        private const string ReCurvatureName = "_ReCurvature";

        public static float? GetWorldLength(Renderer renderer) {
            foreach (var material in renderer.sharedMaterials) {
                if (!IsDps(material)
                    || !material.TryGetFloatFast("_Length", out var length)
                    || !(length > 0) || float.IsInfinity(length)) continue;
                // DPS length uses renderer-local units.
                return length * renderer.owner().worldScale.x;
            }
            return null;
        }

        public static bool IsDps(Material mat) {
            if (mat == null) return false;
            var shader = mat.shader;
            if (!shader) return false;
            if (shader.name == "Raliv/Penetrator") return true; // Raliv
            if (shader.name.Contains("XSToon") && shader.name.Contains("Penetrator")) return true; // XSToon w/ Raliv
            if (mat.GetPropertyType(RalivPenetratorEnabledName) != null &&
                mat.TryGetFloatFast(RalivPenetratorEnabledName, out var enabled) &&
                enabled > 0) return true; // Poiyomi 7 w/ Raliv
            if (shader.name.Contains("DPS") && mat.GetPropertyType(ReCurvatureName) != null) return true; // UnityChanToonShader w/ Raliv
            return false;
        }
    }
}
