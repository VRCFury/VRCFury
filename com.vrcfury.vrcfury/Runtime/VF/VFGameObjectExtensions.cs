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
    }
}
