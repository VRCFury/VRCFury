using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public static class MaterialExtensions {
        public static ShaderUtil.ShaderPropertyType? GetPropertyType(this Material mat, string propertyName) {
            if (mat.shader == null) return null;
            foreach (var i in Enumerable.Range(0, ShaderUtil.GetPropertyCount(mat.shader))) {
                if (ShaderUtil.GetPropertyName(mat.shader, i) == propertyName) {
                    return ShaderUtil.GetPropertyType(mat.shader, i);
                }
            }
            return null;
        }
    }
}
