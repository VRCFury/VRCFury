using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Menu;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class WorldPlayModeHook {
        private const string TriggerObjectName = "__vrcf_play_mode_trigger";
        private static bool triggerAddedThisPlaymode = false;
        private static bool appliedThisPlayMode = false;

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            VRCFuryComponent._OnValidate = () => {
                if (Application.isPlaying && !appliedThisPlayMode && !triggerAddedThisPlaymode && PlayModeMenuItem.Get()) {
                    triggerAddedThisPlaymode = true;
                    RescanOnStartComponent.Create();
                }
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                triggerAddedThisPlaymode = false;
                appliedThisPlayMode = false;
                TmpFilePackage.Cleanup();
            }
        }

        public class BuildCallback : VrcfWorldPreprocessor {
            protected override int order => int.MinValue;
            protected override void Process(Scene scene) {
                appliedThisPlayMode = true;
            }
        }

        [DefaultExecutionOrder(-10000)]
        public class RescanOnStartComponent : VRCFuryPlayComponent {
            private void Start() {
                try {
                    if (!appliedThisPlayMode) {
                        appliedThisPlayMode = true;
                        VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
                    }
                } finally {
                    DestroyImmediate(gameObject);
                }
            }

            public static void Create() {
                if (!Application.isPlaying) return;
                var obj = new GameObject(TriggerObjectName);
                obj.AddComponent<RescanOnStartComponent>();
            }
        }
    }
}
