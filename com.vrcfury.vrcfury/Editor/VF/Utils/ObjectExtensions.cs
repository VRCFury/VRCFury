using System;
using JetBrains.Annotations;

namespace VF.Utils {
    internal static class ObjectExtensions {
        public static T Clone<T>(this T original) where T : UnityEngine.Object {
            return VrcfObjectCloner.Clone(original);
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
