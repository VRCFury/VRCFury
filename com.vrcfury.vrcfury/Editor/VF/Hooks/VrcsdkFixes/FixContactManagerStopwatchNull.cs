using System;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The VRCSDK tries to use a variable before it is initialized, resulting in a consistent NPE the first time
     * you enter play mode after reloading scripts.
     */
    internal static class FixContactManagerStopwatchNull {
        private static readonly Type contactManagerType = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.ContactManager");
        private static readonly FieldInfo stopwatchField = contactManagerType?
            .GetField("_stopwatch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (contactManagerType == null || stopwatchField == null) return;
            HarmonyUtils.Patch(
                typeof(FixContactManagerStopwatchNull),
                nameof(Prefix),
                contactManagerType,
                "HandleDynamicsFrameComplete"
            );
        }

        private static bool Prefix(object __instance) {
            var stopwatch = stopwatchField.GetValue(__instance);
            if (stopwatch == null) {
                stopwatchField.SetValue(__instance, Stopwatch.StartNew());
            }
            return true;
        }
    }
}
