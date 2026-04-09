using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * If the VRCSDK cannot reach api2.amplitude.com, it spams the unity console any time you do anything in the VRCSDK
     * dialog. These errors are non-actionable.
     */
    internal static class SuppressAmplitudeErrorsHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(SuppressAmplitudeErrorsHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.LogError)
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix(object __0) {
            if (__0 is string msg && msg.StartsWith("AmplitudeAPI: ")) {
                return false;
            }
            return true;
        }
    }
}
