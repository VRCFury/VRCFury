using UnityEngine;

namespace VF.Builder.Haptics {
    public static class DpsConfigurer {
        private static readonly int RalivPenetratorEnabled = Shader.PropertyToID("_PenetratorEnabled");

        public static bool IsDps(Material mat) {
            if (mat == null) return false;
            if (!mat.shader) return false;
            if (mat.shader.name == "Raliv/Penetrator") return true; // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return true; // XSToon w/ Raliv
            if (mat.HasProperty(RalivPenetratorEnabled) && mat.GetFloat(RalivPenetratorEnabled) > 0) return true; // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return true; // UnityChanToonShader w/ Raliv
            return false;
        }
    }
}
