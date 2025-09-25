using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Builder;

namespace VF.Utils {
    internal static class CollapseUtils {
        private static Action<VFGameObject, bool> _SetExpanded = (o,e) => { };
        private static Func<int[]> _GetExpandedIds = () => new int[] {};

#if UNITY_2022_1_OR_NEWER
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type SceneHierarchyWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneHierarchyWindow");
            public static readonly MethodInfo SetExpanded = SceneHierarchyWindow?.GetMethod("SetExpanded", false);
            public static readonly MethodInfo GetExpandedIDs = SceneHierarchyWindow?.GetMethod("GetExpandedIDs", false);
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) {
                return;
            }

            _SetExpanded = (o, e) => {
                var win = EditorWindowFinder.GetWindows(Reflection.SceneHierarchyWindow).FirstOrDefault();
                if (win == null) return;
                Reflection.SetExpanded.Invoke(win, new object[] { o.GetInstanceID(), e });
            };
            _GetExpandedIds = () => {
                var win = EditorWindowFinder.GetWindows(Reflection.SceneHierarchyWindow).FirstOrDefault();
                if (win == null) return new int[] { };
                return (Reflection.GetExpandedIDs.Invoke(win, new object[] { }) as int[]) ?? new int[] { };
            };
        }
#endif

        public static void SetExpanded(VFGameObject o, bool e) => _SetExpanded(o, e);
        public static int[] GetExpandedIds() => _GetExpandedIds();
    }
}
