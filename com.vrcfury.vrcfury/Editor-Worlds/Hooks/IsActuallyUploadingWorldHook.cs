using VRC.SDKBase.Editor;

namespace VF.Hooks {
    internal static class IsActuallyUploadingWorldHook {
        public static bool Get() {
            return VRC_SdkBuilder.ActiveBuildType != VRC_SdkBuilder.BuildType.None;
        }
    }
}
