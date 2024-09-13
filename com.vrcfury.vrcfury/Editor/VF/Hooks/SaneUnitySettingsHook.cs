using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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
        }

        private static void DoSafe(Action with) {
            try {
                with();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        private static void DisableErrorPause() {
            UnityReflection.Console.SetConsoleErrorPause?.Invoke(false);
        }
 
        private static void TurnOffPause() {
            EditorApplication.isPaused = false;
        }
        
        private static void EnableErrorLogs() {
            if (UnityReflection.Console.LogLevelError != null) {
                UnityReflection.Console.SetFlag?.Invoke(null, new object[] { UnityReflection.Console.LogLevelError, true });
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
    }
}
