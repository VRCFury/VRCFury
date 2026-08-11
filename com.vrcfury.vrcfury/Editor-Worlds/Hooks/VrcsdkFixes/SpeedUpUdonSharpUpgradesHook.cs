using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * UdonSharp's RunAllUpdates has the potential to do tons of asset work, and doesn't enable AssetEditing
     * by default. Wrapping it in an AssetEditing block speeds it up considerably, since it doesn't trigger
     * OnWillSaveAssets after every single change.
     */
    internal static class SpeedUpUdonSharpUpgradesHook {
        private abstract class Reflection : ReflectionHelper {
            private static readonly System.Type UdonSharpEditorManager =
                ReflectionUtils.GetTypeFromAnyAssembly("UdonSharpEditor.UdonSharpEditorManager");
            public static readonly System.Reflection.MethodInfo RunAllUpdates =
                UdonSharpEditorManager?.VFStaticMethod("RunAllUpdates");
            public static readonly HarmonyUtils.PatchObj PatchRunAllUpdates = HarmonyUtils.Patch(
                typeof(SpeedUpUdonSharpUpgradesHook),
                nameof(OnRunAllUpdates),
                UdonSharpEditorManager,
                "RunAllUpdates"
            );
        }

        private static bool isWrapping;

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchRunAllUpdates.apply();
        }

        private static bool OnRunAllUpdates(object __0) {
            if (isWrapping) return true;

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                isWrapping = true;
                try {
                    Reflection.RunAllUpdates.Invoke(null, new[] { __0 });
                } finally {
                    isWrapping = false;
                }
            });
            return false;
        }
    }
}

