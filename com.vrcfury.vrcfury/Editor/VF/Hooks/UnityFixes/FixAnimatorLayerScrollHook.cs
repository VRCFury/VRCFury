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

            var original = LayerControllerView.GetMethod(
                "ResetUI",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] {},
                null
            );

            var prefix = typeof(FixAnimatorLayerScrollHook).GetMethod(nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(FixAnimatorLayerScrollHook).GetMethod(nameof(Postfix),
                BindingFlags.Static | BindingFlags.NonPublic);

            HarmonyUtils.Patch(original, prefix);
            HarmonyUtils.Patch(original, postfix, true);
        }

        private static Vector2 savedScroll;

        private static void Prefix(object __instance) {
            savedScroll = (Vector2)LayerControllerView_m_LayerScroll.GetValue(__instance);
        }

        private static void Postfix(object __instance) {
            LayerControllerView_m_LayerScroll.SetValue(__instance, savedScroll);
        }
    }
}
