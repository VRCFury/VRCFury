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
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type contactManagerType = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.ContactManager");
            public static readonly FieldInfo stopwatchField = contactManagerType?
                .VFField("_stopwatch");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            HarmonyUtils.Patch(
                typeof(FixContactManagerStopwatchNull),
                nameof(Prefix),
                Reflection.contactManagerType,
                "HandleDynamicsFrameComplete"
            );
        }

        private static bool Prefix(object __instance) {
            var stopwatch = Reflection.stopwatchField.GetValue(__instance);
            if (stopwatch == null) {
                Reflection.stopwatchField.SetValue(__instance, Stopwatch.StartNew());
            }
            return true;
        }
    }
}

