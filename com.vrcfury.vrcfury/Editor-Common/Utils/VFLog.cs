using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VF.Utils {
    internal static class VFLog {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Action<string, string, int, int> LogInfo =
                typeof(Debug).GetMatchingDelegate<Action<string, string, int, int>>("LogInfo");
            public static readonly Action<string, string, int, int> LogWarning =
                typeof(Debug).GetMatchingDelegate<Action<string, string, int, int>>("LogWarning");
        }

        public static void Warning(
            string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        ) {
            message = "[VRCFury] " + message;
            if (Reflection.LogWarning != null) {
                Reflection.LogWarning(message, file, line, 0);
            } else {
                Debug.LogWarning(message);
            }
        }

        public static void Info(
            string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        ) {
            message = "[VRCFury] " + message;
            if (Reflection.LogInfo != null) {
                Reflection.LogInfo(message, file, line, 0);
            } else {
                Debug.Log(message);
            }
        }
    }
}
