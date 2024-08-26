using UnityEngine;

namespace VF.Builder {
    internal static class GameObjects {
        public static VFGameObject Create(
            string name,
            VFGameObject parent = null,
            VFGameObject useTransformFrom = null,
            bool removeFromPhysbones = true
        ) {
            var obj = new GameObject(name).asVf();
            if (useTransformFrom) {
                obj.SetParent(useTransformFrom, false);
                if (parent != null) {
                    obj.SetParent(parent, true);
                } else {
                    obj.SetParent(null, true);
                }
            } else if (parent != null) {
                obj.SetParent(parent, false);
            }

            if (removeFromPhysbones) {
                PhysboneUtils.RemoveFromPhysbones(obj, true);
            }

            obj.EnsureAnimationSafeName();
            return obj;
        }
    }
}
