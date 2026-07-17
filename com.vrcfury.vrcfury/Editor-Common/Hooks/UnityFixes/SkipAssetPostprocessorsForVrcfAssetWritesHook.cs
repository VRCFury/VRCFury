using System;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    internal static class SkipAssetPostprocessorsForVrcfAssetWritesHook {
        private static int suppressPostprocessDepth;

        public struct SuppressionScope : IDisposable {
            public void Dispose() {
                suppressPostprocessDepth--;
            }
        }

        public static SuppressionScope Suppress() {
            suppressPostprocessDepth++;
            return new SuppressionScope();
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
