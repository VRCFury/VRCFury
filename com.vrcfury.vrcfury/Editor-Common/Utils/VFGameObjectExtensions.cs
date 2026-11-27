using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Component;

namespace VF.Utils {
    internal static class VFGameObjectExtensions {
        public static VFGameObject owner(this UnityEngine.Component component) {
            return component is VRCFuryComponent vrcfury ? vrcfury.gameObject : component.gameObject;
        }
        [CanBeNull]
        public static VFGameObject asVf(this GameObject gameObject) {
            return gameObject;
        }
        [CanBeNull]
        public static VFGameObject asVf(this Transform transform) {
            return transform;
        }

        public static IEnumerable<VFGameObject> AsVf(this IEnumerable<GameObject> source) {
            return source.Select(t => t.asVf());
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
