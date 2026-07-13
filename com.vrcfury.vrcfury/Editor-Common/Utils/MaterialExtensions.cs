using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Utils {
    internal static class MaterialExtensions {
        public static ShaderPropertyType? GetPropertyType(this Material mat, string propertyName) {
            if (mat == null || mat.shader == null) return null;
            return mat.shader.GetPropertyType(propertyName);
        }
    }
}
