using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class WorldPlayModeHook {
        private static bool ranPreprocessors = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad() {
            if (!ranPreprocessors) {
                ranPreprocessors = true;
                VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            }
        }

        public class BuildCallback : VrcfWorldPreprocessor {
            protected override int order => int.MinValue;
            protected override void Process(Scene scene) {
                ranPreprocessors = true;
                TmpFilePackage.Cleanup();
            }
        }
    }
}
