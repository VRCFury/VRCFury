using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * If the VRCSDK cannot reach api2.amplitude.com, it spams the unity console any time you do anything in the VRCSDK
     * dialog. These errors are non-actionable.
     */
    internal static class SuppressAmplitudeErrorsHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var original = typeof(Debug).GetMethod(nameof(Debug.LogError), BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(object) }, null);
            var prefix = typeof(SuppressAmplitudeErrorsHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            HarmonyUtils.Patch(original, prefix);
        }

        private static bool Prefix(object __0) {
            if (__0 is string msg && msg.StartsWith("AmplitudeAPI: ")) {
                return false;
            }
            return true;
        }
    }
}
