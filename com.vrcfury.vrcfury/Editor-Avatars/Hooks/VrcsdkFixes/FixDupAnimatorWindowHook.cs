using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class FixDupAnimatorWindowHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
            public static readonly PropertyInfo AnimatorControllerTool_animatorController = AnimatorControllerTool?
                .VFProperty("animatorController");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixDupAnimatorWindowHook),
                nameof(Prefix),
                "AvatarParameterDriverEditor",
                "GetCurrentController"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }
        
        public static AnimatorController GetPreviewedAnimatorController() {
            var tool = EditorWindowFinder.GetWindows(Reflection.AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return null;
            return Reflection.AnimatorControllerTool_animatorController.GetValue(tool) as AnimatorController;
        }
    
        private static bool Prefix(ref AnimatorController __result) {
            __result = GetPreviewedAnimatorController();
            return false;
        }
    }
}

