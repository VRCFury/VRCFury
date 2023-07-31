using System;
using UnityEditor;

namespace VF.Utils {
    public static class EditorCurveBindingExtensions {
        /**
         * Used to make sure that two instances of EditorCurveBinding equal each other,
         * even if they have different discrete settings, etc
         */
        public static EditorCurveBinding Normalize(this EditorCurveBinding binding) {
            return EditorCurveBinding.FloatCurve(binding.path, binding.type, binding.propertyName);
        }
    }
}
