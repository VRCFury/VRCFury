using UnityEditor;
using UnityEngine;

namespace VF.Builder {
    public static class GameObjects {
        public static Transform Create(
            string name,
            Transform parent,
            Transform useTransformFrom = null,
            bool removeFromPhysbones = true
        ) {
            var transform = new GameObject(name).transform;
            if (useTransformFrom) {
                transform.SetParent(useTransformFrom, false);
                if (parent != null) {
                    transform.SetParent(parent, true);
                } else {
                    transform.transform.parent = null;
                }
            } else if (parent != null) {
                transform.SetParent(parent, false);
            }

            if (removeFromPhysbones) {
                PhysboneUtils.RemoveFromPhysbones(transform, true);
            }

            return transform;
        }

        public static void Activate(Transform t) {
            t.gameObject.SetActive(true);
        }
        public static void Deactivate(Transform t) {
            t.gameObject.SetActive(false);
        }
        public static string GetName(Transform t) {
            return t.gameObject.name;
        }
        public static string GetName(UnityEngine.Component t) {
            return GetName(t.transform);
        }
        public static string GetName(GameObject t) {
            return GetName(t.transform);
        }
        public static void SetName(Transform t, string name) {
            t.gameObject.name = name;
        }
        public static void SetName(GameObject t, string name) {
            t.name = name;
        }
        public static T AddComponent<T>(Transform t) where T : UnityEngine.Component {
            return t.gameObject.AddComponent<T>();
        }

        public static string GetPath(Transform t) {
            var root = t.root;
            return GetName(root) + "/" + AnimationUtility.CalculateTransformPath(t, root);
        }
    }
}
