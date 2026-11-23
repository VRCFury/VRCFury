using System;

namespace VF.Utils.Controller {
    internal static class VFParameterRewriteSettings {
        [ThreadStatic] private static int suppressCopyDriverSourceRewriteCount;

        public static bool ShouldRewriteCopyDriverSources => suppressCopyDriverSourceRewriteCount <= 0;

        public static void WithoutCopyDriverSourceRewrites(Action action) {
            suppressCopyDriverSourceRewriteCount++;
            try {
                action();
            } finally {
                suppressCopyDriverSourceRewriteCount--;
            }
        }
    }
}
