using System.Collections.Generic;

namespace VF.Utils {
    internal static class IListExtensions {
        public static T GetOrDefault<T>(this IList<T> list, int index, T defaultValue = default) {
            if (list == null) return defaultValue;
            if (index < 0 || index >= list.Count) return defaultValue;
            return list[index];
        }
    }
}
