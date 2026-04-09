using System;
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
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type LayerControllerView = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.LayerControllerView");
            public static readonly FieldInfo LayerControllerView_m_LayerScroll = LayerControllerView?
                .VFField("m_LayerScroll");
            public static readonly HarmonyUtils.PatchObj PrefixPatch = HarmonyUtils.Patch(
                typeof(FixAnimatorLayerScrollHook),
                nameof(Prefix),
                LayerControllerView,
                "ResetUI"
            );
            public static readonly HarmonyUtils.PatchObj FinalizerPatch = HarmonyUtils.Patch(
                typeof(FixAnimatorLayerScrollHook),
                nameof(Finalizer),
                LayerControllerView,
                "ResetUI",
                patchMode: HarmonyUtils.PatchMode.Finalizer
            );
        }
        
        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            if (Reflection.LayerControllerView_m_LayerScroll.FieldType != typeof(Vector2)) return;
            Reflection.PrefixPatch.apply();
            Reflection.FinalizerPatch.apply();
        }

        private static Vector2 savedScroll;

        private static void Prefix(object __instance) {
            savedScroll = (Vector2)Reflection.LayerControllerView_m_LayerScroll.GetValue(__instance);
        }

        private static void Finalizer(object __instance) {
            Reflection.LayerControllerView_m_LayerScroll.SetValue(__instance, savedScroll);
        }
    }
}

