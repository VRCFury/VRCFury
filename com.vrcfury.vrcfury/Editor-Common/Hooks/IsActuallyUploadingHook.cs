using System.Diagnostics;
using System.Linq;
using UnityEditor;
#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
using VRC.SDKBase.Editor;
#endif

namespace VF.Hooks {
    internal static class IsActuallyUploadingHook {

        public static bool Get() {
#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
            if (VRC_SdkBuilder.ActiveBuildType != VRC_SdkBuilder.BuildType.None) return true;
            // Multi-platform builds can leave ActiveBuildType unset, including after platform switches.
            return VRCSdkControlPanel.window != null
                && VRCSdkControlPanel.TryGetBuilder<IVRCSdkBuilderApi>(out var builder)
                && builder.BuildState == SdkBuildState.Building;
#else
            return LegacyGet();
#endif
        }

        private static bool? cachedIsUploading;
        private static bool LegacyGet() {
            if (cachedIsUploading == null) {
                cachedIsUploading = new StackTrace().GetFrames()?.Any(frame => {
                    var methodName = frame.GetMethod().Name;
                    return methodName == "ExportCurrentAvatarResource"
                           || methodName == "ExportCurrentSceneResource";
                }) ?? false;
                EditorApplication.delayCall += () => cachedIsUploading = null;
            }
            return cachedIsUploading.Value;
        }
    }
}
