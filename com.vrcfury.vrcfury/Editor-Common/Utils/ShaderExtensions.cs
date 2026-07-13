using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Utils {
    internal static class ShaderExtensions {
        public const ShaderPropertyType StPropertyType = (ShaderPropertyType)995;
        private static readonly Dictionary<Shader, Dictionary<string, ShaderPropertyType>> ShaderProperties =
            new Dictionary<Shader, Dictionary<string, ShaderPropertyType>>();

        private static void ClearCacheForAsset(string path) {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null) ShaderProperties.Remove(shader);
        }

        private class ShaderAssetPostprocessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths
            ) {
                foreach (var path in importedAssets) {
                    ClearCacheForAsset(path);
                }
            }
        }

        public static ShaderPropertyType? GetPropertyType(this Shader shader, string propertyName) {
            if (shader == null) return null;
            if (!ShaderProperties.TryGetValue(shader, out var properties)) {
                properties = new Dictionary<string, ShaderPropertyType>();
                foreach (var i in Enumerable.Range(0, shader.GetPropertyCount())) {
                    properties[shader.GetPropertyName(i)] = shader.GetPropertyType(i);
                }
                ShaderProperties[shader] = properties;
            }
            if (properties.TryGetValue(propertyName, out var type)) return type;
            if (propertyName.EndsWith("_ST")) {
                var basePropertyName = propertyName.Substring(0, propertyName.Length - 3);
                if (properties.TryGetValue(basePropertyName, out var baseType) && baseType == ShaderPropertyType.Texture) {
                    return StPropertyType;
                }
            }
            return null;
        }
    }
}
