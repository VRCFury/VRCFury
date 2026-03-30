using JetBrains.Annotations;

namespace VF.Utils {
    internal static class ComponentExtensions {
        [CanBeNull]
        public static VFConstraint AsConstraint(this UnityEngine.Component component) {
            return VFConstraint.CreateOrNull(component);
        }
    }
}
