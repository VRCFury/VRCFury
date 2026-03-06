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
        private abstract class Av3EmuReflection : ReflectionHelper {
            public static readonly FieldInfo NonLocalSyncInterval = Av3EmuAnimatorFixHook.LyumaAv3Runtime?.VFField("NonLocalSyncInterval");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(Av3EmuSyncTimeFixHook),
                nameof(Prefix),
                Av3EmuAnimatorFixHook.LyumaAv3Runtime,
                "Awake"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Av3EmuReflection>()) return;
            Av3EmuReflection.Patch.apply();
        }

        private static bool Prefix(object __instance) {
            Av3EmuReflection.NonLocalSyncInterval.SetValue(__instance, 0.1f);
            return true;
        }
    }
}
