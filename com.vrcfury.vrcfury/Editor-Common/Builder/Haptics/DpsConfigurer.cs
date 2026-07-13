using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class DpsConfigurer {
        private static readonly int RalivPenetratorEnabled = Shader.PropertyToID("_PenetratorEnabled");
        private const string RalivPenetratorEnabledName = "_PenetratorEnabled";
        private const string ReCurvatureName = "_ReCurvature";

        public static bool IsDps(Material mat) {
            if (mat == null) return false;
            var shader = mat.shader;
            if (!shader) return false;
            if (shader.name == "Raliv/Penetrator") return true; // Raliv
            if (shader.name.Contains("XSToon") && shader.name.Contains("Penetrator")) return true; // XSToon w/ Raliv
            if (mat.GetPropertyType(RalivPenetratorEnabledName) != null && mat.GetFloat(RalivPenetratorEnabled) > 0) return true; // Poiyomi 7 w/ Raliv
            if (shader.name.Contains("DPS") && mat.GetPropertyType(ReCurvatureName) != null) return true; // UnityChanToonShader w/ Raliv
            return false;
        }
    }
}
