using System.Collections;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class SerializedPropertyExtensions {
        // Returns the innermost array index, including for fields inside an array element.
        public static int? GetArrayIndex(this SerializedProperty prop) {
            var path = prop.propertyPath;
            var indexStart = path.LastIndexOf('[') + 1;
            var indexEnd = path.IndexOf(']', indexStart);
            return indexStart > 0 && indexEnd > indexStart
                && int.TryParse(path.Substring(indexStart, indexEnd - indexStart), out var index)
                    ? index : null;
        }

        [CanBeNull]
        public static object GetObject(this SerializedProperty prop) {
            var path = prop.propertyPath.Replace(".Array.data[", ".[");
            object obj = prop.serializedObject.targetObject;

            foreach (var slug in path.Split('.')) {
                if (obj == null) return null;
                if (slug.StartsWith("[")) {
                    if (!(obj is IList e)) return null;
                    if (!int.TryParse(slug.Substring(1, slug.Length - 2), out var index)) return null;
                    if (index < 0 || index >= e.Count) return null;
                    obj = e[index];
                } else {
                    var type = obj.GetType();
                    var f = type.VFField(slug);
                    if (f == null) return null;
                    obj = f.GetValue(obj);
                }
            }
            return obj;
        }

        [CanBeNull]
        public static T GetComponent<T>(this SerializedProperty prop) where T : UnityEngine.Component {
            return (prop.objectReferenceValue as GameObject).NullSafe()?.GetComponent<T>();
        }

        /** For some reason, unity occasionally breaks and return non-Objects from objectReferenceValue somehow. */
        [CanBeNull]
        public static Object GetObjectReferenceValueSafe(this SerializedProperty prop) {
            if (prop.objectReferenceValue == null) return null;
            if (!(prop.objectReferenceValue is object systemObj)) return null;
            if (!(systemObj is Object unityObj)) return null;
            return unityObj;
        }

        public static bool NextFast(this SerializedProperty prop) {
            var path = prop.propertyPath;
            if (path == "") return prop.Next(true);
            var type = prop.propertyType;
            if (type == SerializedPropertyType.String) return prop.Next(false);

            // Params in an animator controller
            if (path == "m_AnimatorParameters") return prop.Next(false);
            // Float arrays in an animation clip
            if (path == "m_FloatCurves") return prop.Next(false);
            if (path == "m_EulerEditorCurves") return prop.Next(false);
            if (path == "m_EditorCurves") return prop.Next(false);
            if (path == "m_ClipBindingConstant") return prop.Next(false);
            // Numeric properties in a material
            if (path == "m_SavedProperties.m_Ints") return prop.Next(false);
            if (path == "m_SavedProperties.m_Floats") return prop.Next(false);
            if (path == "m_SavedProperties.m_Colors") return prop.Next(false);

            if (prop.isArray) {
                if (prop.arraySize == 0) return prop.Next(false);
                var firstType = prop.GetArrayElementAtIndex(0).propertyType;
                return prop.Next(firstType == SerializedPropertyType.ObjectReference
                                 || firstType == SerializedPropertyType.ExposedReference
                                 || firstType == SerializedPropertyType.Generic
                                 || firstType == SerializedPropertyType.ManagedReference);
            }

            return prop.Next(type == SerializedPropertyType.Generic
                             || type == SerializedPropertyType.ManagedReference);
        }
    }
}
