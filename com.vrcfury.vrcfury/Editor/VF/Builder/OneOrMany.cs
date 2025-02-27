using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace VF.Builder {
    internal class OneOrMany<T> {
        public readonly IList<T> _raw;

        private OneOrMany(IList<T> list) {
            _raw = list;
        }
        
        public static implicit operator OneOrMany<T>(T d) => new OneOrMany<T>(new [] { d });
        public static implicit operator OneOrMany<T>(T[] d) => new OneOrMany<T>(d);
        public static implicit operator OneOrMany<T>(List<T> d) => new OneOrMany<T>(d);
    }

    internal static class OneOrManyExtensions {
        // This is an extension so that it works even if the OneOrMany is null
        public static IList<T> Get<T>([CanBeNull] this OneOrMany<T> oneOrMany) {
            return oneOrMany?._raw ?? Array.Empty<T>();
        }
    }
}
