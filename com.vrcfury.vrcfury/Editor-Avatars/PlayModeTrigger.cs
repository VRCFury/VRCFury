using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Hooks;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEngine.SceneManagement;

namespace VF {
    internal static class PlayModeTrigger {
        [VFInit]
        private static void Init() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                VRCFuryBuildContext.Run(() => {
                    TmpDirService.Cleanup();
                });
            }
        }

        internal class SceneProcessor : IProcessSceneWithReport {
            public int callbackOrder => int.MinValue + 100;

            public void OnProcessScene(Scene scene, BuildReport report) {
                if (!Application.isPlaying) return;
                if (!PlayModeMenuItem.Get()) return;
                ProcessScene(scene);
            }
        }

        private static void ProcessScene(Scene scene) {
            var activeSockets = new List<VRCFuryHapticSocket>();
            var activePlugs = new List<VRCFuryHapticPlug>();
            foreach (var rootObj in scene.GetRootGameObjects()) {
                ProcessTree(rootObj, activeSockets, activePlugs);
            }
            ProcessSps(activeSockets, activePlugs);
        }

        private static void ProcessTree(
            VFGameObject obj,
            List<VRCFuryHapticSocket> activeSockets,
            List<VRCFuryHapticPlug> activePlugs
        ) {
            if (obj == null) return;
            if (IsAv3EmulatorClone(obj)) return;

            var avatar = obj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                ProcessOnStartComponent.Process(obj, () => ProcessAvatar(obj));
                return;
            }

            var socket = obj.GetComponent<VRCFuryHapticSocket>();
            if (socket != null) {
                if (obj.activeInHierarchy) {
                    activeSockets.Add(socket);
                    return;
                }
                ProcessOnStartComponent.Process(obj, () => {
                    if (socket != null) {
                        ProcessSps(new[] { socket }, Array.Empty<VRCFuryHapticPlug>());
                    }
                });
                return;
            }

            var plug = obj.GetComponent<VRCFuryHapticPlug>();
            if (plug != null) {
                if (obj.activeInHierarchy) {
                    activePlugs.Add(plug);
                    return;
                }
                ProcessOnStartComponent.Process(obj, () => {
                    if (plug != null) {
                        ProcessSps(Array.Empty<VRCFuryHapticSocket>(), new[] { plug });
                    }
                });
                return;
            }

            foreach (var child in obj.Children()) {
                ProcessTree(child, activeSockets, activePlugs);
            }
        }

        private static void ProcessSps(
            IList<VRCFuryHapticSocket> sockets,
            IList<VRCFuryHapticPlug> plugs
        ) {
            VRCFuryBuildContext.Run(() => {
                SpsBakeAndSave.Run(sockets, plugs);
            });
        }

        private static void ProcessAvatar(VFGameObject obj) {
            if (!RunPreprocessorsOnlyOncePatch.ShouldStartPreprocessors(obj)) return;
            if (!VRCFuryBuilder.ShouldRun(obj)) return;

            var orig = obj.Clone();
            orig.name = obj.name;
            obj.name += "(Clone)";
            VRCBuildPipelineCallbacks.OnPreprocessAvatar(obj);
            obj.name = orig.name;
            orig.Destroy();
        }

        public static bool IsAv3EmulatorClone(VFGameObject obj) {
            return obj.name.Contains("(ShadowClone)")
                   || obj.name.Contains("(MirrorReflection)");
        }

        [DefaultExecutionOrder(-10000)]
        public class ProcessOnStartComponent : VRCFuryPlayComponent {
            public Action action;

            private void Start() {
                if (!Application.isPlaying || !PlayModeMenuItem.Get()) return;
                try {
                    action?.Invoke();
                } finally {
                    DestroyImmediate(this);
                }
            }

            public static void Process(VFGameObject obj, Action action) {
                if (!Application.isPlaying) return;
                if (obj == null) return;
                if (obj.activeInHierarchy) {
                    action?.Invoke();
                    return;
                }
                var component = obj.AddComponent<ProcessOnStartComponent>();
                component.action = action;
            }
        }
    }
}
