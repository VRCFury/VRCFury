using System;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Unity's native Material getters/setters may apply MaterialPropertyDrawers, which can be extremely slow
     * and is not needed for VRCFury's build-time property reads/writes.
     */
    internal static class SuppressMaterialPropertyDrawersHook {
        private static int suppressDepth;

        public struct SuppressionScope : IDisposable {
            public void Dispose() {
                suppressDepth--;
            }
        }

        public static SuppressionScope Suppress() {
            suppressDepth++;
            return new SuppressionScope();
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(SuppressMaterialPropertyDrawersHook),
                nameof(Prefix),
                typeof(UnityEditor.MaterialEditor),
                "ApplyMaterialPropertyDrawersFromNative"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            return suppressDepth <= 0;
        }
    }
}
