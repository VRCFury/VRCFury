using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class MaterialExtensions {
        public const ShaderUtil.ShaderPropertyType StPropertyType = (ShaderUtil.ShaderPropertyType)995;
        
        public static ShaderUtil.ShaderPropertyType? GetPropertyType(this Material mat, string propertyName) {
            if (mat.shader == null) return null;
            foreach (var i in Enumerable.Range(0, ShaderUtil.GetPropertyCount(mat.shader))) {
                var name = ShaderUtil.GetPropertyName(mat.shader, i);
                var type = ShaderUtil.GetPropertyType(mat.shader, i);
                if (name == propertyName) return type;
                if (propertyName.EndsWith("_ST") && name == propertyName.Substring(0, propertyName.Length - 3) && type == ShaderUtil.ShaderPropertyType.TexEnv) {
                    return StPropertyType;
                }
            }
            return null;
        }
    }
}
