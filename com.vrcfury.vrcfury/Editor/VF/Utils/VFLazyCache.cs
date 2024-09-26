using System;
using System.Collections.Generic;

namespace VF.Utils {
    internal class VFLazyCache<KeyT,ValueT> : Dictionary<KeyT,ValueT> {
        private readonly Func<KeyT, ValueT> factory;

        public VFLazyCache(Func<KeyT, ValueT> factory) {
            this.factory = factory;
        }

        public ValueT GetOrCreate(KeyT key) {
            if (TryGetValue(key, out var exists)) return exists;
            return this[key] = factory(key);
        }
    }
}
