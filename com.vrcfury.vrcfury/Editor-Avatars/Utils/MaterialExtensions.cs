using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Utils {
    internal static class MaterialExtensions {
        public const ShaderPropertyType StPropertyType = (ShaderPropertyType)995;
        
        public static ShaderPropertyType? GetPropertyType(this Material mat, string propertyName) {
            if (mat.shader == null) return null;
            foreach (var i in Enumerable.Range(0, mat.shader.GetPropertyCount())) {
                var name = mat.shader.GetPropertyName(i);
                var type = mat.shader.GetPropertyType(i);
                if (name == propertyName) return type;
                if (propertyName.EndsWith("_ST") && name == propertyName.Substring(0, propertyName.Length - 3) && type == ShaderPropertyType.Texture) {
                    return StPropertyType;
                }
            }
            return null;
        }
    }
}
