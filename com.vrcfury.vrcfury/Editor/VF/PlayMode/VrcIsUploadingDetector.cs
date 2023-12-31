using UnityEditor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.PlayMode {
    [InitializeOnLoad]
    public class VrcIsUploadingDetector : IVRCSDKPostprocessAvatarCallback {
        private const string AboutToUploadKey = "vrcf_vrcAboutToUpload";

        public int callbackOrder => int.MaxValue;
        public void OnPostprocessAvatar() {
            EditorPrefs.SetFloat(AboutToUploadKey, Now());
        }

        static VrcIsUploadingDetector() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static float Now() {
            return (float)EditorApplication.timeSinceStartup;
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingPlayMode) {
                EditorPrefs.DeleteKey(AboutToUploadKey);
            }
        }

        public static bool IsProbablyUploading() {
            if (!EditorPrefs.HasKey(AboutToUploadKey)) return false;
            var aboutToUploadTime = EditorPrefs.GetFloat(AboutToUploadKey, 0);
            var now = Now();
            return aboutToUploadTime <= now && aboutToUploadTime > now - 10;
        }
    }
}
