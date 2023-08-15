using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Component {
    public static class JsonSerializerState {
        private static List<Object> objects = new List<Object>();

        public static void Clear() {
            objects.Clear();
        }

        public static Object GetObject(int id) {
            if (id >= 0 && id < objects.Count) {
                return objects[id];
            }
            return null;
        }

        public static void SetObjects(IEnumerable<Object> objs) {
            objects.Clear();
            objects.AddRange(objs);
        }
        
        public static IImmutableList<Object> GetObjects() {
            return objects.ToImmutableList();
        }

        public static int GetId(Object obj) {
            if (obj == null) return -1;
            var id = objects.IndexOf(obj);
            if (id >= 0) return id;
            objects.Add(obj);
            return objects.Count - 1;
        }
    }
}
