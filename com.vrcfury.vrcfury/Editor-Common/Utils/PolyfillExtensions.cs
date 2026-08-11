using System.Collections.Generic;

namespace VF.Utils {
    internal static class PolyfillExtensions {
#if ! UNITY_2021_2_OR_NEWER
        public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dict, K key) {
            return dict.TryGetValue(key, out var value) ? value : default;
        }
        public static bool TryPop<T>(this Stack<T> stack, out T result) {
            if (stack.Count > 0) {
                result = stack.Pop();
                return true;
            }
            result = default;
            return false;
        }
#endif
    }
}
