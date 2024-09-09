using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;

namespace VF.Utils {
    internal static class ObjectExtensions {
        public static readonly VFMultimapList<UnityEngine.Object, string> cloneReasons
            = new VFMultimapList<UnityEngine.Object, string>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.update += () => cloneReasons.Clear();
        }

        public static T Clone<T>(this T original, string reason = null) where T : UnityEngine.Object {
            var clone = VrcfObjectCloner.Clone(original);
            if (clone != original) {
                foreach (var r in cloneReasons.Get(original)) {
                    cloneReasons.Put(clone, r);
                }
            }
            if (reason != null) {
                cloneReasons.Put(clone, reason);
            }
            return clone;
        }

        /**
         * Converts destroyed Objects ("fake-null") into "real" null values which can use null-coalescing
         */
        [CanBeNull]
        public static T NullSafe<T>([CanBeNull] this T obj) where T : UnityEngine.Object {
            return obj == null ? null : obj;
        }
    }
}
