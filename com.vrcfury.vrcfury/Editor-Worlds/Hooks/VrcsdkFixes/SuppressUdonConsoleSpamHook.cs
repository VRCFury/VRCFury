using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * ClientSim and various common udon projects spam messages to console that are irrelevant to world devs and
     * cannot be turned off. This suppresses them.
     */
    internal static class SuppressUdonConsoleSpamHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchLogWarning = HarmonyUtils.Patch(
                typeof(SuppressUdonConsoleSpamHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.LogWarning)
            );
            public static readonly HarmonyUtils.PatchObj PatchLogWarningFormat = HarmonyUtils.Patch(
                typeof(SuppressUdonConsoleSpamHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.LogWarningFormat)
            );
            public static readonly HarmonyUtils.PatchObj PatchLog = HarmonyUtils.Patch(
                typeof(SuppressUdonConsoleSpamHook),
                nameof(Prefix),
                typeof(Debug),
                nameof(Debug.Log)
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchLogWarning.apply();
            Reflection.PatchLogWarningFormat.apply();
            Reflection.PatchLog.apply();
        }

        private static bool Prefix(object __0) {
            if (__0 == null) return true;
            var msg = __0.ToString();

            // When Instantiate is called on an object that contains an udon behaviour, clientsim prints an error that it's
            // "cleaning up" the "uninitialized" helpers (that were cloned as part of the Instantiate)
            if (msg.Contains("Destroying uninitialized Helper")) return false;

            // ClientSim prints "Recovered x Network IDs from objectname" every time you enter play mode
            if (msg.Contains("Network IDs from")) return false;

            // VideoTXL prints these every time scripts reload
            if (msg == "AssemblyReload") return false;
            if (msg.StartsWith("[VideoTXL] Found") && msg.Contains("ScreenManagers in scene")) return false;

            // VideoTXL prints this every time the scene loads
            if (msg == "SceneLoaded") return false;

            // ClientSim prints this every time any udon script calls FindComponentInPlayerObjects
            if (msg.Contains("possible matches for") && msg.Contains("in player objects for")) return false;

            return true;
        }
    }
}
