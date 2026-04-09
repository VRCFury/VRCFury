using UnityEngine;

namespace VF.Utils {
    internal static class AnimatorControllerParameterExtensions {
        public static float GetDefaultValueAsFloat(this AnimatorControllerParameter p) {
            if (p.type == AnimatorControllerParameterType.Bool) return p.defaultBool ? 1 : 0;
            if (p.type == AnimatorControllerParameterType.Int) return p.defaultInt;
            if (p.type == AnimatorControllerParameterType.Float) return p.defaultFloat;
            return 0;
        }
    }
}
