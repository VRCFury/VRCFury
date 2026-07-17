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
            return VRC_SdkBuilder.ActiveBuildType != VRC_SdkBuilder.BuildType.None;
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
