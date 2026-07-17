using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VF.Utils {
    internal class VFMultimap<KeyType,ValueType,CollectionType>
        : IEnumerable<KeyValuePair<KeyType,ValueType>>
        where CollectionType : ICollection<ValueType>, new()
    {
        private readonly Dictionary<KeyType, CollectionType> data = new Dictionary<KeyType, CollectionType>();
        
        public CollectionType Get(KeyType key) {
            return data.TryGetValue(key, out var list) ? list : new CollectionType();
        }

        public void Put(KeyType key, ValueType value) {
            if (data.TryGetValue(key, out var list)) {
                list.Add(value);
            } else {
                data[key] = new CollectionType { value };
            }
        }

        public IEnumerable<KeyType> GetKeys() {
            return data.Keys;
        }

        public bool ContainsKey(KeyType key) {
            return data.ContainsKey(key);
        }

        public bool ContainsValue(ValueType val) {
            return data.Values.Any(v => v.Contains(val));
        }

        public IEnumerator<KeyValuePair<KeyType, ValueType>> GetEnumerator() {
            using (var ie = data.GetEnumerator()) {
                while (ie.MoveNext()) {
                    var current = ie.Current;
                    var key = current.Key;
                    var collection = current.Value;
                    foreach (var value in collection) {
                        yield return new KeyValuePair<KeyType, ValueType>(key, value);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Clear() {
            data.Clear();
        }
    }
    
    internal class VFMultimapList<A, B> : VFMultimap<A, B, List<B>> {
    }

    internal class VFMultimapSet<A, B> : VFMultimap<A, B, HashSet<B>> {
    }
}
