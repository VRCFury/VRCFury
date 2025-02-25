using System.Collections.Generic;

namespace VF.Builder {
    public class OneOrMany<T> {
        public readonly IList<T> list;

        private OneOrMany(IList<T> list) {
            this.list = list;
        }
        
        public static implicit operator OneOrMany<T>(T d) => new OneOrMany<T>(new [] { d });
        public static implicit operator OneOrMany<T>(T[] d) => new OneOrMany<T>(d);
        public static implicit operator OneOrMany<T>(List<T> d) => new OneOrMany<T>(d);
    }
}
