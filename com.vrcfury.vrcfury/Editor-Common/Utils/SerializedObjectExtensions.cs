using JetBrains.Annotations;
using UnityEditor;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;

namespace VF.Utils {
    internal static class SerializedObjectExtensions {
        [CanBeNull]
        public static UnityEngine.Component GetComponent(this SerializedObject obj) {
            return (obj.targetObject as UnityEngine.Component).NullSafe();
        }
        
        [CanBeNull]
        public static VFGameObject GetGameObject(this SerializedObject obj) {
            return obj.GetComponent()?.owner();
        }

        [CanBeNull]
        public static FeatureModel GetVrcFuryFeature(this SerializedObject obj) {
            return (obj.GetComponent() as VRCFury)?.content;
        }

        /**
         * Iterates through Unity serialized properties while skipping branches that cannot contain
         * useful object references.
         *
         * This intentionally does not enter:
         * - strings, because Unity exposes them as arrays of characters
         * - primitive/value arrays, such as floats, ints, bools, vectors, colors, etc
         * - other leaf properties with no children
         *
         * This does enter:
         * - generic serialized classes/structs
         * - managed references
         * - arrays whose first element is an object reference, exposed reference, generic value, or managed reference
         *
         * Empty arrays are not entered because they cannot currently contain object references.
         */
        public static System.Collections.Generic.IEnumerable<SerializedProperty> IterateFast(this SerializedObject serialized) {
            var prop = serialized.GetIterator();
            while (prop.NextFast()) {
                if (prop.propertyPath == "m_CorrespondingSourceObject") continue;
                if (prop.propertyPath == "m_PrefabInstance") continue;
                yield return prop;
            }
        }
    }
}
