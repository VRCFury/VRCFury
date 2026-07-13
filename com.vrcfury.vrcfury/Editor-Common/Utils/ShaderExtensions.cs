using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Utils {
    internal static class ShaderExtensions {
        public const ShaderPropertyType StPropertyType = (ShaderPropertyType)995;
        private static readonly Dictionary<Shader, Dictionary<string, ShaderProperty>> ShaderProperties =
            new Dictionary<Shader, Dictionary<string, ShaderProperty>>();

        private struct ShaderProperty {
            public ShaderPropertyType type;
            public int index;
        }

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
            var properties = shader.GetProperties();
            if (properties.TryGetValue(propertyName, out var prop)) return prop.type;
            if (propertyName.EndsWith("_ST")) {
                var basePropertyName = propertyName.Substring(0, propertyName.Length - 3);
                if (properties.TryGetValue(basePropertyName, out var baseProp) && baseProp.type == ShaderPropertyType.Texture) {
                    return StPropertyType;
                }
            }
            return null;
        }

        public static int GetPropertyIndex(this Shader shader, string propertyName) {
            if (shader == null) return -1;
            return shader.GetProperties().TryGetValue(propertyName, out var prop) ? prop.index : -1;
        }

        public static bool HasProperty(this Shader shader, string propertyName) {
            return shader != null && shader.GetProperties().ContainsKey(propertyName);
        }

        public static IEnumerable<string> GetPropertyNames(this Shader shader, ShaderPropertyType type) {
            if (shader == null) return Enumerable.Empty<string>();
            return shader.GetProperties()
                .Where(pair => pair.Value.type == type)
                .Select(pair => pair.Key);
        }

        private static Dictionary<string, ShaderProperty> GetProperties(this Shader shader) {
            if (!ShaderProperties.TryGetValue(shader, out var properties)) {
                properties = new Dictionary<string, ShaderProperty>();
                foreach (var i in Enumerable.Range(0, shader.GetPropertyCount())) {
                    properties[shader.GetPropertyName(i)] = new ShaderProperty {
                        type = shader.GetPropertyType(i),
                        index = i
                    };
                }
                ShaderProperties[shader] = properties;
            }
            return properties;
        }
    }
}
