using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    /**
     * If you build a test copy without a blueprint id, the VRCSDK will display an error
     * "Attempted to load the data for an avatar we do not own, clearing blueprint ID" after the build
     * every time.
     */
    public class SuppressBlueprintWarningHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var original = typeof(Debug).GetMethod(nameof(Debug.LogError), BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(object), typeof(Object) }, null);
            var prefix = typeof(SuppressBlueprintWarningHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            HarmonyUtils.Patch(original, prefix);
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
