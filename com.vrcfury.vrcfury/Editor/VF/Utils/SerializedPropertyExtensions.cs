using System.Collections;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;

namespace VF.Utils {
    public static class SerializedPropertyExtensions {
        [CanBeNull]
        public static object GetObject(this SerializedProperty prop) {
            var path = prop.propertyPath.Replace(".Array.data[", ".[");
            object obj = prop.serializedObject.targetObject;

            foreach (var slug in path.Split('.')) {
                if (slug.StartsWith("[")) {
                    if (!(obj is IList e)) return null;
                    if (!int.TryParse(slug.Substring(1, slug.Length - 2), out var index)) return null;
                    if (index < 0 || index >= e.Count) return null;
                    obj = e[index];
                } else {
                    var type = obj.GetType();
                    var f = type.GetField(slug, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (f == null) return null;
                    obj = f.GetValue(obj);
                }
            }
            return obj;
        }
    }
}
