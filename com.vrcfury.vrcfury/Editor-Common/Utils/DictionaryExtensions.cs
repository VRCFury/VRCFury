using System;
using System.Collections.Generic;

namespace VF.Utils {
    internal static class DictionaryExtensions {
        public static V GetOrCreate<K, V>(this Dictionary<K, V> dict, K key, Func<V> create) {
            if (dict.TryGetValue(key, out var value)) return value;
            return dict[key] = create();
        }

        public static V GetOrCreate<K, V, TState>(
            this Dictionary<K, V> dict,
            K key,
            TState state,
            Func<K, TState, V> create
        ) {
            if (dict.TryGetValue(key, out var value)) return value;
            return dict[key] = create(key, state);
        }
    }
}
