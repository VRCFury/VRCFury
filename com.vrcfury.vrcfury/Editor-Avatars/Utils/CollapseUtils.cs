using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class CollapseUtils {
        private static Action<VFGameObject, bool> _SetExpanded = (o,e) => { };
        private static Func<ISet<VFGameObject>> _GetExpanded = () => new HashSet<VFGameObject>();

#if UNITY_2022_1_OR_NEWER
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type SceneHierarchyWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneHierarchyWindow");
            public static readonly MethodInfo SetExpanded = SceneHierarchyWindow?.VFMethod("SetExpanded");
            public static readonly MethodInfo GetExpandedIDs = SceneHierarchyWindow?.VFMethod("GetExpandedIDs");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) {
                return;
            }

            _SetExpanded = (o, e) => {
                var win = EditorWindowFinder.GetWindows(Reflection.SceneHierarchyWindow).FirstOrDefault();
                if (win == null) return;
                GameObject go = o;
                Reflection.SetExpanded.Invoke(win, new object[] {
#if UNITY_6000_0_OR_NEWER
                    go.GetEntityId()
#else
                    go.GetInstanceID()
#endif
                    , e
                });
            };
            _GetExpanded = () => {
                var win = EditorWindowFinder.GetWindows(Reflection.SceneHierarchyWindow).FirstOrDefault();
                if (win == null) return new HashSet<VFGameObject>();
#if UNITY_6000_0_OR_NEWER
                var ids = (Reflection.GetExpandedIDs.Invoke(win, new object[] { }) as EntityId[]) ?? new EntityId[] { };
                return ids.Select(id => EditorUtility.EntityIdToObject(id) as GameObject).NotNull().AsVf().ToImmutableHashSet();
#else
                var ids = (Reflection.GetExpandedIDs.Invoke(win, new object[] { }) as int[]) ?? new int[] { };
                return ids.Select(id => EditorUtility.InstanceIDToObject(id) as GameObject).NotNull().AsVf().ToImmutableHashSet();
#endif
            };
        }
#endif

        public static void SetExpanded(VFGameObject o, bool e) => _SetExpanded(o, e);
        public static ISet<VFGameObject> GetExpanded() => _GetExpanded();
    }
}
