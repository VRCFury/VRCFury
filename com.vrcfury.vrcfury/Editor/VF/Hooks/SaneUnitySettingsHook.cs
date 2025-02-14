using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * Prevents the user from accidentally doing dumb things in their unity settings, then breaking their project.
     */
    internal static class SaneUnitySettingsHook {
        [InitializeOnLoadMethod]
        private static void Apply() {
            DoSafe(DisableErrorPause);
            DoSafe(TurnOffPause);
            DoSafe(EnableErrorLogs);
            DoSafe(ExitPlayWhenCompiling);
            DoSafe(DisableMeshOptimization);
        }

        private static void DoSafe(Action with) {
            try {
                with();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }
        
        private abstract class ConsoleReflection : ReflectionHelper {
            public static readonly Type ConsoleWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ConsoleWindow");
            public delegate void SetConsoleErrorPause_(bool enabled);
            public static readonly SetConsoleErrorPause_ SetConsoleErrorPause = ConsoleWindow?.GetMatchingDelegate<SetConsoleErrorPause_>("SetConsoleErrorPause");
            public static readonly MethodInfo SetFlag = ConsoleWindow?.GetMethod("SetFlag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type ConsoleFlags = ConsoleWindow?.GetNestedType("ConsoleFlags", BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly object LogLevelError = ConsoleFlags != null ? Enum.Parse(ConsoleFlags, "LogLevelError") : null;
        }

        private static void DisableErrorPause() {
            ConsoleReflection.SetConsoleErrorPause?.Invoke(false);
        }
 
        private static void TurnOffPause() {
            EditorApplication.isPaused = false;
        }
        
        private static void EnableErrorLogs() {
            if (ConsoleReflection.LogLevelError != null) {
                ConsoleReflection.SetFlag?.Invoke(null, new object[] { ConsoleReflection.LogLevelError, true });
            }
        }
   
        /**
         * If you recompile while in play mode, the VRCSDK freaks out and starts
         * spamming console errors regarding physbones
         */
        private static void ExitPlayWhenCompiling() {
            // If "Compilation During Play" is set to "Recompile And Continue Playing",
            // change it to "Recompile After Finished Playing"
            if (EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0) == 0) {
                EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            }
        }

        private static void DisableMeshOptimization() {
            PlayerSettings.stripUnusedMeshComponents = false;
        }
    }
}
