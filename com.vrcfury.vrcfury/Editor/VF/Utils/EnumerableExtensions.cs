using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class EnumerableExtensions {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> source) {
            return source.Where(e => {
                // Always use the proper != overload, instead of always using the one from System.Object
                dynamic d = e;
                return d != null;
            });
        }
        
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> with) {
            foreach (var item in source) {
                with(item);
            }
        }

        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize) {
            return source
                .Select((e,i) => (e,i))
                .GroupBy(x => x.i / chunkSize)
                .Select(g => g.Select(x => x.e));
        }

        public static string Join(this IEnumerable<string> source, string separator) {
            return string.Join(separator, source);
        }
        public static string Join(this IEnumerable<string> source, char separator) {
#if UNITY_2022_1_OR_NEWER
            return string.Join(separator, source);
#else
            return string.Join(separator+"", source);
#endif
        }
    }
}
