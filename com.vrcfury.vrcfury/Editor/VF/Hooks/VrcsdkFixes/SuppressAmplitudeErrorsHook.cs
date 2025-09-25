using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * If the VRCSDK cannot reach api2.amplitude.com, it spams the unity console any time you do anything in the VRCSDK
     * dialog. These errors are non-actionable.
     */
    internal static class SuppressAmplitudeErrorsHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(SuppressAmplitudeErrorsHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.LogError)
            );
        }

        private static bool Prefix(object __0) {
            if (__0 is string msg && msg.StartsWith("AmplitudeAPI: ")) {
                return false;
            }
            return true;
        }
    }
}
