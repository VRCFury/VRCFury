using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
#if VRC_NEW_PUBLIC_SDK
using VRC.SDK3A.Editor;
#endif

namespace VF.Hooks {
    internal static class IsActuallyUploadingHook {
        
        private static bool actuallyUploading = false;
        public static bool Get() {
            return actuallyUploading;
        }

#if VRC_NEW_PUBLIC_SDK
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (_, _2) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (_3, _4) => actuallyUploading = true;
                    builder.OnSdkBuildFinish += (_3, _4) => actuallyUploading = false;
                }
            };
        }
#endif

        private static void LegacyUpdate() {
            if (Application.isPlaying) {
                actuallyUploading = false;
            } else {
                var stack = new StackTrace().GetFrames();
                actuallyUploading = stack.Any(frame =>
                    (frame.GetMethod().DeclaringType?.FullName ?? "").Contains("VRC.SDK3.Builder.VRCAvatarBuilder"));
                EditorApplication.delayCall += () => actuallyUploading = false;
            }
        }

        internal class LegacyPreprocessor : VrcfAvatarPreprocessor {
            protected override int order => int.MinValue;

            protected override void Process(VFGameObject _) {
#if ! VRC_NEW_PUBLIC_SDK
                LegacyUpdate();
#endif
            }
        }
    }
}
