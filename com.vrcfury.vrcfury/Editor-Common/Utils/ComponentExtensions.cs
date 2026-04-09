using JetBrains.Annotations;
using UnityEngine;

namespace VF.Utils {
    internal static class ComponentExtensions {
        [CanBeNull]
        public static VFConstraint AsConstraint(this UnityEngine.Component component) {
            return VFConstraint.CreateOrNull(component);
        }

        public static void Destroy(this UnityEngine.Component c) {
            if (c.owner().GetComponents<UnityEngine.Component>().Length == 2 && c.owner().childCount == 0)
                c.owner().Destroy();
            else
                Object.DestroyImmediate(c);
        }
    }
}
