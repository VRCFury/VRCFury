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
    }
}
