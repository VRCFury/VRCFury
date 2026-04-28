using System.Diagnostics;
using System.Linq;
#if VRC_NEW_PUBLIC_SDK
using VRC.SDKBase.Editor;
#endif

namespace VF.Hooks {
    internal static class IsActuallyUploadingHook {

        public static bool Get() {
#if VRC_NEW_PUBLIC_SDK
            return VRC_SdkBuilder.ActiveBuildType != VRC_SdkBuilder.BuildType.None;
#else
            return LegacyGet();
#endif
        }

        private static bool LegacyGet() {
            return new StackTrace()
                .GetFrames()?
                .Any(frame => (frame.GetMethod().DeclaringType?.FullName ?? "")
                    .Contains("VRC.SDK3.Builder.VRCAvatarBuilder"))
                ?? false;
        }
    }
}
