using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Utils {
    internal static class ShaderExtensions {
        public static ShaderPropertyType? GetPropertyType(this Shader shader, string propertyName) {
            foreach (var i in Enumerable.Range(0, shader.GetPropertyCount())) {
                if (shader.GetPropertyName(i) == propertyName) {
                    return shader.GetPropertyType(i);
                }
            }
            return null;
        }
    }
}