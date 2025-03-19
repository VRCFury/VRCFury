using System.IO;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Automatically disable domain reload and scene reload to speed up entering play mode.
     * This only happens once, so if the user changes it back afterward, we don't touch it.
     */
    internal static class SetPlayModeSettingsHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(() => {
                var tmpFolder = TmpFilePackage.GetPathNullable();
                if (tmpFolder == null) {
                    EditorApplication.delayCall += Init;
                    return;
                }

                var markerPath = $"{tmpFolder}/PlayModeSettings";
                if (File.Exists(markerPath)) {
                    return;
                }
                File.Create(markerPath).Close();

                if (!EditorSettings.enterPlayModeOptionsEnabled) {
                    EditorSettings.enterPlayModeOptionsEnabled = true;
                    EditorSettings.enterPlayModeOptions =
                        EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
                }
            }, 5000);
        }
    }
}
