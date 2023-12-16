using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    public static class EnumerableExtensions {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> source) {
            // Have to cast to Object to use its special != operator :(
            return source.Where(e => (e is Object o) ? o != null : e != null);
        }
    }
}
