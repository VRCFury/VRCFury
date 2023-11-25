using System.Collections.Generic;
using System.Linq;

namespace VF.Utils {
    public static class EnumerableExtensions {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> source) {
            return source.Where(e => e != null);
        }
    }
}
