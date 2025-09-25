using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Due to a bug in mono ( https://github.com/mono/mono/issues/20968 ),
     * Assembly.GetName() may throw an exception when called on an assembly created by Harmony
     * if the system locale is set to non-utf8, non-english, and the project path contains non-english characters.
     * This is because mono attempts to parse the non-utf8 characters as utf8, which fails.
     *
     * VRC.Tools calls GetName on every assembly in the project, which means if ANY assembly causes this issue,
     * the VRCSDK builder window fails to load.
     *
     * To fix this, we patch Assembly.GetName to never throw.
     */
    internal static class FixVrcsdkNonenglishLocaleCrashHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(FixVrcsdkNonenglishLocaleCrashHook),
                nameof(Prefix),
                typeof(Assembly),
                nameof(Assembly.GetName)
            );
        }

        private static bool Prefix(Assembly __instance, ref AssemblyName __result) {
            try {
                __instance.GetName(false);
            } catch (Exception) {
                __result = typeof(Application).Assembly.GetName(false);
                return false;
            }

            return true;
        }
    }
}
