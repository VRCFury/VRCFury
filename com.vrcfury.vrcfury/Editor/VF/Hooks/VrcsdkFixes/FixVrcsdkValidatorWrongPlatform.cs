using UnityEditor;
#if VRC_NEW_PUBLIC_SDK
using VRC.SDK3A.Editor;
#endif

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The vrcsdk internally uses EditorUserBuildSettings.selectedBuildTargetGroup,
     * which changes unexpectedly when you select platform options on a texture asset.
     * They should have used EditorUserBuildSettings.activeBuildTarget.
     * We fix this by correcting the build target group to match whenever the vrcsdk dialog opens or
     * a build begins.
     */
    internal static class FixVrcsdkValidatorWrongPlatform {
        [InitializeOnLoadMethod]
        private static void Init() {
            SyncBuildTargetGroup();
#if VRC_NEW_PUBLIC_SDK
            VRCSdkControlPanel.OnSdkPanelEnable += (sender, e) => {
                SyncBuildTargetGroup();
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (sender2, target) => {
                        SyncBuildTargetGroup();
                    };
                }
            };
#endif
        }

        private static void SyncBuildTargetGroup() {
            EditorUserBuildSettings.selectedBuildTargetGroup =
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        }
    }
}
