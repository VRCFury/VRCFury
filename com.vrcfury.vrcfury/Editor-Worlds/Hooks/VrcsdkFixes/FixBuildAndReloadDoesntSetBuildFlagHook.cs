#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
using System.Threading.Tasks;
using UnityEditor;
using VF.Utils;
using VRC.SDKBase.Editor;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * When you build and reload a world, the VRCSDK leaves VRC_SdkBuilder.ActiveBuildType set to None,
     * which breaks all uploading detections.
     * https://feedback.vrchat.com/sdk-bug-reports/p/vrc-sdkbuilderactivebuildtype-remains-unset-during-build-and-reload-for-worlds
     */
    internal class FixBuildAndReloadDoesntSetBuildFlagHook {
        public abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchBuild = HarmonyUtils.Patch(
                ("VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder", "Build"),
                (typeof(FixBuildAndReloadDoesntSetBuildFlagHook), nameof(Prefix))
            );
            public static readonly HarmonyUtils.PatchObj PatchBuildPostfix = HarmonyUtils.Patch(
                ("VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder", "Build"),
                (typeof(FixBuildAndReloadDoesntSetBuildFlagHook), nameof(Postfix)),
                HarmonyUtils.PatchMode.Postfix
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchBuild.apply();
            Reflection.PatchBuildPostfix.apply();
        }

        private static bool weSet = false;
        private static void Prefix() {
            weSet = false;
            if (VRC_SdkBuilder.ActiveBuildType == VRC_SdkBuilder.BuildType.None) {
                weSet = true;
                VRC_SdkBuilder.ActiveBuildType = VRC_SdkBuilder.BuildType.Test;
            }
        }

        private static void Postfix(ref Task __result) {
            __result = WrapResult(__result);
        }

        private static async Task WrapResult(Task originalTask) {
            try {
                await originalTask;
            } finally {
                if (weSet) {
                    VRC_SdkBuilder.ActiveBuildType = VRC_SdkBuilder.BuildType.None;
                }
            }
        }
    }
}
#endif
