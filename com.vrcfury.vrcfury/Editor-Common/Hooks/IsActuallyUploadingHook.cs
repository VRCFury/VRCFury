using System.Diagnostics;
using System.Linq;
using UnityEditor;
#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
using VRC.SDKBase.Editor;
#endif

namespace VF.Hooks {
    internal static class IsActuallyUploadingHook {

        public static bool IsPublishing() {
#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
            var buildType = VRC_SdkBuilder.ActiveBuildType;
            if (buildType != VRC_SdkBuilder.BuildType.None) return buildType == VRC_SdkBuilder.BuildType.Publish;
            // On later platforms ActiveBuildType can be unset. The SDK keeps its multi-platform
            // publish flag in SessionState across reloads. A standalone bundle build also enters Building.
            return SessionState.GetBool("VRCSdkControlPanel.MultiPlatformBuild", false) && Get();
#else
            // These SDKs cannot publish a new avatar and do not distinguish uploads from local builds.
            return false;
#endif
        }

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
