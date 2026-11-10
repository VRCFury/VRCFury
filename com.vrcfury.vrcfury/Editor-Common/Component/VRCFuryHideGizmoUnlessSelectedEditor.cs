using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Component {
    internal static class VRCFuryHideGizmoUnlessSelectedEditor {
        [VFInit]
        private static void Init() {
            VRCFuryHideGizmoUnlessSelected.IsSelected = obj => Selection.gameObjects.Contains(obj);
            UpdateAll();
            Selection.selectionChanged += UpdateAll;
        }

        private static void UpdateAll() {
#if UNITY_6000_4_OR_NEWER
            var objs = UnityEngine.Object.FindObjectsByType<VRCFuryHideGizmoUnlessSelected>(FindObjectsInactive.Exclude);
#elif UNITY_2022_3_OR_NEWER
            var objs = UnityEngine.Object.FindObjectsByType<VRCFuryHideGizmoUnlessSelected>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var objs = UnityEngine.Object.FindObjectsOfType<VRCFuryHideGizmoUnlessSelected>();
#endif
            foreach (var c in objs) {
                c.OnSelectionChanged();
            }
        }
    }
}
