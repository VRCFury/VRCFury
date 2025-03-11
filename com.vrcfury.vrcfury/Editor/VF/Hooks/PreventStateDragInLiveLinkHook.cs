using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * Dragging a state in an animator by even one single pixel while in play mode totally breaks live link and disables gesture manager.
     * This hook prevents you from accidentally dragging nodes in an animator while live link is active.
     */
    internal class PreventStateDragInLiveLinkHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var original = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.GraphGUI")?.GetMethod(
                "DragNodes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] {},
                null
            );

            var prefix = typeof(PreventStateDragInLiveLinkHook).GetMethod(nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);

            HarmonyUtils.Patch(original, prefix);
        }
        
        private static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
        private static readonly PropertyInfo AnimatorControllerTool_liveLink = AnimatorControllerTool?
            .GetProperty("liveLink", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        private static bool Prefix() {
            if (ShouldBlockDrag()) return false;
            return true;
        }

        private static bool ShouldBlockDrag() {
            if (AnimatorControllerTool_liveLink == null) return false;
            var tool = EditorWindowFinder.GetWindows(AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return false;
            return (bool)AnimatorControllerTool_liveLink.GetValue(tool);
        }
    }
}
