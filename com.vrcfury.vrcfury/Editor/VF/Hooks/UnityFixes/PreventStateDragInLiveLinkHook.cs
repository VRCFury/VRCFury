using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Dragging a state in an animator by even one single pixel while in play mode totally breaks live link and disables gesture manager.
     * This hook prevents you from accidentally dragging nodes in an animator while live link is active.
     */
    internal static class PreventStateDragInLiveLinkHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
            public static readonly PropertyInfo AnimatorControllerTool_liveLink = AnimatorControllerTool?
                .VFProperty("liveLink");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(PreventStateDragInLiveLinkHook),
                nameof(Prefix),
                "UnityEditor.Graphs.GraphGUI",
                "DragNodes"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            if (ShouldBlockDrag()) return false;
            return true;
        }

        private static bool ShouldBlockDrag() {
            if (Event.current == null) return false;
            if (Event.current.type != EventType.KeyDown && Event.current.type != EventType.MouseDrag) return false;
            var tool = EditorWindowFinder.GetWindows(Reflection.AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return false;
            return (bool)Reflection.AnimatorControllerTool_liveLink.GetValue(tool);
        }
    }
}

