using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public static class RendererExtensions {
        public static ShaderUtil.ShaderPropertyType? GetPropertyType(this Renderer renderer, string propertyName) {
            return renderer.sharedMaterials
                .NotNull()
                .Select(m => m.GetPropertyType(propertyName))
                .Where(type => type != null)
                .DefaultIfEmpty(null)
                .FirstOrDefault();
        }
    }
}
