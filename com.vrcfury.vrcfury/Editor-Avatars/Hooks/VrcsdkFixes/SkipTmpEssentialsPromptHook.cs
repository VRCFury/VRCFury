using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The SDK reads TMP_Settings.instance during project setup, which opens Unity's
     * TMP Essentials importer even in avatar projects that don't use TextMesh Pro.
     */
    internal static class SkipTmpEssentialsPromptHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(SkipTmpEssentialsPromptHook),
                nameof(Prefix),
                "VRC.Editor.EnvConfig",
                "EnsureTMPImported"
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool Prefix() => false;
    }
}
