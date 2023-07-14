using UnityEngine;

namespace VF.Builder {
    public class GameObjects {
        public static VFGameObject Create(
            string name,
            VFGameObject parent,
            VFGameObject useTransformFrom = null,
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
    }
}