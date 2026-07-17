using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Component {
    internal static class VRCFuryHideGizmoUnlessSelectedEditor {
        [VFInit]
        private static void Init() {
            UpdateAll();
            Selection.selectionChanged += UpdateAll;
            EditorApplication.hierarchyChanged += UpdateAll;
        }

        private static void UpdateAll() {
#if UNITY_6000_0_OR_NEWER
            var objs = UnityEngine.Object.FindObjectsByType<VRCFuryHideGizmoUnlessSelected>(FindObjectsInactive.Exclude);
#elif UNITY_2022_1_OR_NEWER
            var objs = UnityEngine.Object.FindObjectsByType<VRCFuryHideGizmoUnlessSelected>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var objs = UnityEngine.Object.FindObjectsOfType<VRCFuryHideGizmoUnlessSelected>();
#endif
            foreach (var c in objs) {
                c.OnSelectionChanged(Selection.gameObjects.Contains(c.gameObject));
            }
        }
    }
}
