using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Whenever you delete or move a layer in a controller, unity scrolls the list to the top for no reason.
     * This hook fixes that issue.
     */
    internal static class FixAnimatorLayerScrollHook {
        private static readonly Type LayerControllerView = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.LayerControllerView");
        private static readonly FieldInfo LayerControllerView_m_LayerScroll = LayerControllerView?
            .GetField("m_LayerScroll", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        
        [InitializeOnLoadMethod]
        private static void Init() {
            if (LayerControllerView_m_LayerScroll == null) return;
            if (LayerControllerView_m_LayerScroll.FieldType != typeof(Vector2)) return;

            HarmonyUtils.Patch(
                typeof(FixAnimatorLayerScrollHook),
                nameof(Prefix),
                LayerControllerView,
                "ResetUI"
            );
            HarmonyUtils.Patch(
                typeof(FixAnimatorLayerScrollHook),
                nameof(Finalizer),
                LayerControllerView,
                "ResetUI",
                patchMode: HarmonyUtils.PatchMode.Finalizer
            );
        }

        private static Vector2 savedScroll;

        private static void Prefix(object __instance) {
            savedScroll = (Vector2)LayerControllerView_m_LayerScroll.GetValue(__instance);
        }

        private static void Finalizer(object __instance) {
            LayerControllerView_m_LayerScroll.SetValue(__instance, savedScroll);
        }
    }
}
