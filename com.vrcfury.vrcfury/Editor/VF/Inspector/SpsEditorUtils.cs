using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Utils;

namespace VF.Inspector {
    internal static class SpsEditorUtils {
        public static VisualElement AutoHapticIdProp<T>(
            SerializedProperty prop,
            string label,
            T target,
            VFGameObject owner,
            Func<VFGameObject, IEnumerable<T>> getItems,
            Func<T, string> getPreferredId
        ) {
            return VRCFuryEditorUtils.AutoIdProp(
                prop,
                label,
                () => HapticUtils.GetActualId(target, owner, getItems, getPreferredId)
            );
        }
    }
}
