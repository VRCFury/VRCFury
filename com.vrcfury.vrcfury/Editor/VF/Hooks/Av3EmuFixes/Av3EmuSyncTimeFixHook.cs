using UnityEditor;
using VF.Utils;

namespace VF.Hooks.Av3EmuFixes {
    /**
     * Av3emu defaults to 0.2s sync time, which doesn't work with parameter compressor, because the game
     * actually uses 0.1s. We change the default to make sure it works with parameter compressor.
     */
    internal static class Av3EmuSyncTimeFixHook {
        [InitializeOnLoadMethod]
        private static void Init() { 
            HarmonyUtils.Patch(
                typeof(Av3EmuSyncTimeFixHook),
                nameof(Prefix),
                "Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime",
                "Awake",
                warnIfMissing: false
            );
            HarmonyUtils.Patch(
                typeof(Av3EmuSyncTimeFixHook),
                nameof(Prefix),
                "LyumaAv3Runtime",
                "Awake",
                warnIfMissing: false
            );
        }

        private static bool Prefix(object __instance) {
            var field = __instance.GetType().GetField("NonLocalSyncInterval");
            if (field != null) {
                field.SetValue(__instance, 0.1f);
            }
            return true;
        }
    }
}
