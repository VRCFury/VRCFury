using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * "Assets/Create/U# Script" fails if the destination file is in a package that is outside project root
     * This fixes it.
     * https://feedback.vrchat.com/sdk-bug-reports/p/assets-create-u-script-fails-if-destination-file-is-outside-project-root
     */
    internal static class UdonSharpCreateScriptSavePanelHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(UdonSharpCreateScriptSavePanelHook),
                nameof(Prefix),
                "UdonSharpEditor.UdonSharpSettings",
                "SanitizeScriptFilePath"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static void Prefix(ref string __0) {
            if (string.IsNullOrWhiteSpace(__0)) return;
#if UNITY_2022_1_OR_NEWER
            __0 = FileUtil.GetLogicalPath(__0);
#endif
        }
    }
}
