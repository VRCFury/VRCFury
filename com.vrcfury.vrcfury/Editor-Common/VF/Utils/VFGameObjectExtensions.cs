using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace VF.Builder {
    internal static class VFGameObjectExtensions {
        public static VFGameObject owner(this UnityEngine.Component component) {
            // Some components (ahem VrcFury) override gameObject for some cases, so we need to make sure
            // we call the overridden version
            dynamic d = component;
            return d.gameObject;
        }
        [CanBeNull]
        public static VFGameObject asVf(this GameObject gameObject) {
            return gameObject;
        }
        [CanBeNull]
        public static VFGameObject asVf(this Transform transform) {
            return transform;
        }

        public static IEnumerable<VFGameObject> AsVf(this IEnumerable<Transform> source) {
            return source.Select(t => t.asVf());
        }
        public static IEnumerable<VFGameObject> Children(this IEnumerable<VFGameObject> source) {
            return source.SelectMany(t => t.Children());
        }
        public static IEnumerable<VFGameObject> AllChildren(this IEnumerable<VFGameObject> source) {
            return source.SelectMany(t => t.GetSelfAndAllChildren());
        }
    }
}
