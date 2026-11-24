using System.Reflection;
using VF.Utils;

namespace VF.Hooks.Av3EmuFixes {
    /**
     * Av3emu defaults to 0.2s sync time, which doesn't work with parameter compressor, because the game
     * actually uses 0.1s. We change the default to make sure it works with parameter compressor.
     */
    internal static class Av3EmuSyncTimeFixHook {
        [ReflectionHelperOptional]
        private abstract class Av3EmuReflection : ReflectionHelper {
            public static readonly FieldInfo NonLocalSyncInterval = Av3EmuTypes.Runtime?.VFField("NonLocalSyncInterval");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(Av3EmuSyncTimeFixHook),
                nameof(Prefix),
                Av3EmuTypes.Runtime,
                "Awake"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Av3EmuReflection>()) return;
            Av3EmuReflection.Patch.apply();
        }

        private static void Prefix(object __instance) {
            if (Equals(Av3EmuReflection.NonLocalSyncInterval.GetValue(__instance), 0.2f)) {
                Av3EmuReflection.NonLocalSyncInterval.SetValue(__instance, 0.1f);
            }
        }
    }
}
