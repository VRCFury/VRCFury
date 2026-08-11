

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Features;
using VF.Menu;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Play mode already runs IProcessSceneWithReport's, but it doesn't run IVRCSDKBuildRequestedCallback's
     * like a regular VRChat build. This makes it run them too.
     */
    internal static class RunBuildRequestInPlayModeHook {
        private static bool ranThisFrame = false;

        public class BuildRequestedCallback : IVRCSDKBuildRequestedCallback {
            public int callbackOrder => int.MinValue;
            public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType) {
                ranThisFrame = true;
                EditorApplication.delayCall += () => ranThisFrame = false;
                return true;
            }
        }

        public class SceneProcessor : IProcessSceneWithReport {
            public int callbackOrder => int.MinValue + 100;
            public void OnProcessScene(Scene scene, BuildReport report) {
                if (!Application.isPlaying) return;
                if (!PlayModeMenuItem.Get()) return;
                if (ranThisFrame) return;
                ranThisFrame = true;
                VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
                // This contains only vrcsdk internals, and most of them don't work in play mode anyways
                //VRCBuildPipelineCallbacks.OnPreprocessScene(scene);
            }
        }
    }
}
