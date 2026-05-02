using UnityEditor;
using VF.Utils;

namespace VF.Component {
    internal static class VRCFuryHideGizmoUnlessSelectedEditor {
        [VFInit]
        private static void Init() {
            VRCFuryHideGizmoUnlessSelected.isSelected = c =>
                c.gameObject == Selection.activeGameObject;
        }
    }
}
