using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Hooks {
    /**
     * Prevents the user from accidentally doing dumb things in their unity settings, then breaking their project.
     */
    public static class SaneUnitySettingsHook {
        [InitializeOnLoadMethod]
        public static void Apply() {
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
            var ConsoleWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ConsoleWindow");
            if (ConsoleWindow == null) return;
            var SetConsoleErrorPause = ConsoleWindow.GetMethod("SetConsoleErrorPause",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (SetConsoleErrorPause == null) return;
            SetConsoleErrorPause.Invoke(null, new object[] { false });
        }

        private static void TurnOffPause() {
            EditorApplication.isPaused = false;
        }
        
        private static void EnableErrorLogs() {
            var ConsoleWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ConsoleWindow");
            if (ConsoleWindow == null) return;
            var SetFlag = ConsoleWindow.GetMethod("SetFlag",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (SetFlag == null) return;
            var ConsoleFlags = ConsoleWindow.GetNestedType("ConsoleFlags", BindingFlags.Public | BindingFlags.NonPublic);
            if (ConsoleFlags == null) return;
            var LogLevelError = Enum.Parse(ConsoleFlags, "LogLevelError");
            if (LogLevelError == null) return;
            SetFlag.Invoke(null, new object[] { LogLevelError, true });
        }
    }
}
