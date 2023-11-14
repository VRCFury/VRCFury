using System.Collections.Generic;
using System.Linq;

namespace VF.Utils {
    public class VFMultimap<A,B> {
        private Dictionary<A, List<B>> data = new Dictionary<A, List<B>>();
        
        public IList<B> Get(A key) {
            return data.TryGetValue(key, out var list) ? list : new List<B>();
        }

        public void Put(A key, B value) {
            if (data.TryGetValue(key, out var list)) {
                list.Add(value);
            } else {
                data[key] = new List<B> { value };
            }
        }

        public IEnumerable<A> GetKeys() {
            return data.Keys;
        }
    }
}
