using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * If you build a test copy without a blueprint id, the VRCSDK will display an error
     * "Attempted to load the data for an avatar we do not own, clearing blueprint ID" after the build
     * every time.
     */
    internal static class SuppressBlueprintWarningHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(SuppressBlueprintWarningHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.LogError)
            );
        }

        private static bool Prefix(object __0, Object __1) {
            if (__0 is string msg && msg.Contains("Attempted to load the data for an avatar we do not own")) {
                Debug.Log(msg, __1);
                return false;
            }
            return true;
        }
    }
}
