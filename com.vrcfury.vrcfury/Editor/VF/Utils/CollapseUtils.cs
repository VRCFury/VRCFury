using System;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class CollapseUtils {
        private static Action<VFGameObject, bool> _SetExpanded = (o,e) => { };
        private static Func<int[]> _GetExpandedIds = () => new int[] {};

        [InitializeOnLoadMethod]
        private static void Init() {
            var SceneHierarchyWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneHierarchyWindow");
            var SetExpanded = SceneHierarchyWindow?.GetMethod("SetExpanded", false);
            var GetExpandedIDs = SceneHierarchyWindow?.GetMethod("GetExpandedIDs", false);
            if (SetExpanded == null || GetExpandedIDs == null) {
                Debug.LogError("VRCFury Failed to find hierarchy expansion methods");
                return;
            }

            _SetExpanded = (o, e) => {
                var win = EditorWindow.GetWindow(SceneHierarchyWindow);
                if (win == null) return;
                SetExpanded.Invoke(win, new object[] { o.GetInstanceID(), e });
            };
            _GetExpandedIds = () => {
                var win = EditorWindow.GetWindow(SceneHierarchyWindow);
                if (win == null) return new int[] { };
                return (GetExpandedIDs.Invoke(win, new object[] { }) as int[]) ?? new int[] { };
            };
        }

        public static void SetExpanded(VFGameObject o, bool e) => _SetExpanded(o, e);
        public static int[] GetExpandedIds() => _GetExpandedIds();
    }
}
