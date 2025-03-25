using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class FixDupAnimatorWindowHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(FixDupAnimatorWindowHook),
                nameof(Prefix),
                "AvatarParameterDriverEditor",
                "GetCurrentController"
            );    
        }

        private static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
        private static readonly PropertyInfo AnimatorControllerTool_animatorController = AnimatorControllerTool?
            .GetProperty("animatorController", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        
        public static AnimatorController GetPreviewedAnimatorController() {
            if (AnimatorControllerTool == null || AnimatorControllerTool_animatorController == null) return null;
            var tool = EditorWindowFinder.GetWindows(AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return null;
            return AnimatorControllerTool_animatorController.GetValue(tool) as AnimatorController;
        }
    
        private static bool Prefix(ref AnimatorController __result) {
            __result = GetPreviewedAnimatorController();
            return false;
        }
    }
}
