using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.Av3EmuFixes {
    /**
     * Av3emu defaults to 0.2s sync time, which doesn't work with parameter compressor, because the game
     * actually uses 0.1s. We change the default to make sure it works with parameter compressor.
     */
    internal static class Av3EmuSyncTimeFixHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type LyumaAv3RuntimeType =
                ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime")
                ?? ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Runtime");
            public static readonly FieldInfo NonLocalSyncInterval = LyumaAv3RuntimeType?.VFField("NonLocalSyncInterval");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            HarmonyUtils.Patch(
                typeof(Av3EmuSyncTimeFixHook),
                nameof(Prefix),
                Reflection.LyumaAv3RuntimeType,
                "Awake",
                warnIfMissing: false
            );
        }

        private static bool Prefix(object __instance) {
            Reflection.NonLocalSyncInterval.SetValue(__instance, 0.1f);
            return true;
        }
    }
}
