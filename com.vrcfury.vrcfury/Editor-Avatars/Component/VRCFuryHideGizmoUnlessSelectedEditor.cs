using UnityEditor;

namespace VF.Component {
    internal static class VRCFuryHideGizmoUnlessSelectedEditor {
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCFuryHideGizmoUnlessSelected.isSelected = c =>
                c.gameObject == Selection.activeGameObject;
        }
    }
}
