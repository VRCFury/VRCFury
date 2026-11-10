using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Utils;

namespace VF.Inspector {
    internal static class SpsEditorUtils {
        public static VisualElement MetersWarning() {
            var warning = VRCFuryEditorUtils.Warn("Usually you should use Local rather than Meters. Values in Meters stay fixed in world space and do not scale with the avatar.");
            warning.name = "spsMetersWarning";
            return warning;
        }

        public static VisualElement AutoHapticIdProp<T>(
            SerializedProperty prop,
            string label,
            T target,
            VFGameObject owner,
            Func<VFGameObject, IEnumerable<T>> getItems,
            Func<T, string> getPreferredId,
            bool isOgbId = true
        ) {
            return VRCFuryEditorUtils.AutoIdProp(
                prop,
                label,
                () => HapticUtils.GetActualId(target, owner, getItems,
                    item => isOgbId ? HapticUtils.NormalizeOgbId(getPreferredId(item)) : getPreferredId(item))
            );
        }
    }
}
