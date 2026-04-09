using System;
using System.Collections.Generic;

namespace VF.Utils {
    internal static class DictionaryExtensions {
        public static V GetOrCreate<K, V>(this Dictionary<K, V> dict, K key, Func<V> create) {
            if (dict.TryGetValue(key, out var exists)) return exists;
            return dict[key] = create();
        }
    }
}
