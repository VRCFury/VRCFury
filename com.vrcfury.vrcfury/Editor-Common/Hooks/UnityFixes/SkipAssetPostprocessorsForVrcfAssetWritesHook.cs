using VF.Utils;

namespace VF.Hooks.UnityFixes {
    internal static class SkipAssetPostprocessorsForVrcfAssetWritesHook {
        private static int suppressPostprocessDepth;

        public static void WithPostprocessSuppressed(System.Action action) {
            suppressPostprocessDepth++;
            try {
                action();
            } finally {
                suppressPostprocessDepth--;
            }
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(SkipAssetPostprocessorsForVrcfAssetWritesHook),
                nameof(Prefix),
                "UnityEditor.AssetPostprocessingInternal",
                "PostprocessAllAssets"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            return suppressPostprocessDepth <= 0;
        }
    }
}
