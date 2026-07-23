using System.Collections.Generic;

namespace VF.Utils {
    internal static class IReadOnlyDictionaryExtensions {
        public static TValue GetOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dict,
            TKey key,
            TValue defaultValue = default
        ) {
            if (dict == null || key == null) return defaultValue;
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
