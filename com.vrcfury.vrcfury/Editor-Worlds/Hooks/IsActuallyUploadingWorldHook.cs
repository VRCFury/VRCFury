using UnityEditor;
using VRC.SDK3.Editor;

namespace VF.Hooks {
    internal static class IsActuallyUploadingWorldHook {
        private static bool actuallyUploading = false;
        public static bool Get() {
            return actuallyUploading;
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (_, _2) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkWorldBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (_3, _4) => actuallyUploading = true;
                    builder.OnSdkBuildError += (_3, _4) => actuallyUploading = false;
                    builder.OnSdkBuildFinish += (_3, _4) => actuallyUploading = false;
                }
            };
        }
    }
}
