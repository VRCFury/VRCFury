using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    internal static class SpsSelectionOutlineHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(SpsSelectionOutlineHook),
                nameof(Postfix),
                "UnityEditor.HandleUtility",
#if UNITY_6000_0_OR_NEWER
                "FilterEntityIds",
#else
                "FilterInstanceIDs",
#endif
                patchMode: HarmonyUtils.PatchMode.Postfix
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static void Postfix(
#if UNITY_6000_0_OR_NEWER
            ref EntityId[] __2, ref HashSet<EntityId> __3
#else
            ref int[] __2
#endif
        ) {
            if (__2 == null || __2.Length == 0) return;

            var directlySelected = new HashSet<GameObject>(Selection.gameObjects);

#if UNITY_6000_0_OR_NEWER
            var filtered = new List<EntityId>(__2.Length);
#else
            var filtered = new List<int>(__2.Length);
#endif

            foreach (var instanceId in __2) {
#if UNITY_6000_0_OR_NEWER
                var obj = EditorUtility.EntityIdToObject(instanceId) as Renderer;
#else
                var obj = EditorUtility.InstanceIDToObject(instanceId) as Renderer;
#endif
                if (obj == null) {
                    filtered.Add(instanceId);
                    continue;
                }

                var go = obj.gameObject;
                if (go == null) {
                    filtered.Add(instanceId);
                    continue;
                }

                if (go.GetComponent<VRCFuryHideGizmoUnlessSelected>() != null && !directlySelected.Contains(go)) {
                    continue;
                }

                filtered.Add(instanceId);
            }

            if (filtered.Count == __2.Length) return;
            __2 = filtered.ToArray();
#if UNITY_6000_0_OR_NEWER
            __3.IntersectWith(filtered);
#endif
        }
    }
}
