using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    internal static class FixDupAnimatorWindowHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var methodToPatch = ReflectionUtils.GetTypeFromAnyAssembly("AvatarParameterDriverEditor")?.GetMethod( 
                "GetCurrentController",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { },
                null
            );

            var prefix = typeof(FixDupAnimatorWindowHook).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            HarmonyUtils.Patch(methodToPatch, prefix);    
        }
        
        private static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
        private static readonly PropertyInfo AnimatorControllerTool_animatorController = AnimatorControllerTool?
            .GetProperty("animatorController", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        
        private static AnimatorController GetAnimatorController() {
            if (AnimatorControllerTool == null || AnimatorControllerTool_animatorController == null) return null;
            var tool = Resources.FindObjectsOfTypeAll(AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return null;
            return AnimatorControllerTool_animatorController.GetValue(tool) as AnimatorController;
        }
    
        static bool Prefix(ref AnimatorController __result) {
            __result = GetAnimatorController();
            return false;
        }
    }
}
