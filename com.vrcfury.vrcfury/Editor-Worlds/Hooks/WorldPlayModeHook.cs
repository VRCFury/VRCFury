using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Menu;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class WorldPlayModeHook {
        private const string TriggerObjectName = "__vrcf_play_mode_trigger";
        private static bool ranPreprocessors = false;

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                ranPreprocessors = false;
                TmpFilePackage.Cleanup();
                RescanOnStartComponent.Create();
            }
        }

        public class BuildCallback : VrcfWorldPreprocessor {
            protected override int order => int.MinValue;
            protected override void Process(Scene scene) {
                ranPreprocessors = true;
            }
        }

        [DefaultExecutionOrder(-10000)]
        [ExecuteAlways]
        public class RescanOnStartComponent : VRCFuryPlayComponent {
            private void Start() {
                if (Application.isPlaying && !ranPreprocessors) {
                    ranPreprocessors = true;
                    VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
                }
            }

            private void Awake() {
                // This conditional prevents it from getting nuked in edit mode right after we add it
                // as play mode is starting
                if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                    DestroyImmediate(gameObject);
                }
            }

            public static void Create() {
                var obj = new GameObject(TriggerObjectName);
                obj.AddComponent<RescanOnStartComponent>();
            }
        }
    }
}
