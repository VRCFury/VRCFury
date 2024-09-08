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
    }
}
