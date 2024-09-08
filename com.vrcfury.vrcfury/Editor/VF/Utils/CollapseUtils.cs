using System;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class CollapseUtils {
        private static Action<VFGameObject, bool> _SetExpanded = (o,e) => { };
        private static Func<int[]> _GetExpandedIds = () => new int[] {};

#if UNITY_2022_1_OR_NEWER
        [InitializeOnLoadMethod]
        private static void Init() {
            if (!UnityReflection.IsReady(typeof(UnityReflection.Collapse))) {
                return;
            }

            _SetExpanded = (o, e) => {
                var win = EditorWindow.GetWindow(UnityReflection.Collapse.SceneHierarchyWindow);
                if (win == null) return;
                UnityReflection.Collapse.SetExpanded.Invoke(win, new object[] { o.GetInstanceID(), e });
            };
            _GetExpandedIds = () => {
                var win = EditorWindow.GetWindow(UnityReflection.Collapse.SceneHierarchyWindow);
                if (win == null) return new int[] { };
                return (UnityReflection.Collapse.GetExpandedIDs.Invoke(win, new object[] { }) as int[]) ?? new int[] { };
            };
        }
#endif

        public static void SetExpanded(VFGameObject o, bool e) => _SetExpanded(o, e);
        public static int[] GetExpandedIds() => _GetExpandedIds();
    }
}
