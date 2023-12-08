using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Builder {
    public static class VFGameObjectExtensions {
        public static VFGameObject owner(this UnityEngine.Component component) {
            return component.gameObject;
        }
        public static VFGameObject asVf(this GameObject gameObject) {
            return gameObject;
        }
        public static VFGameObject asVf(this Transform transform) {
            return transform;
        }

        public static IEnumerable<VFGameObject> AsVf(this IEnumerable<Transform> source) {
            return source.Select(t => t.asVf());
        }
        public static IEnumerable<Transform> AsTransform(this IEnumerable<VFGameObject> source) {
            return source.Select(t => t.transform);
        }
        public static IEnumerable<Transform> Children(this IEnumerable<Transform> source) {
            return source.AsVf().SelectMany(t => t.Children()).AsTransform();
        }
    }
}
