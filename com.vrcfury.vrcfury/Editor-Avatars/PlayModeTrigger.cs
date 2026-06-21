using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Exceptions;
using VF.Hooks;
using VF.Inspector;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VF {
    internal class PlayModeTrigger {
        [VFInit]
        private static void Init() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                TmpDirService.Cleanup();
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
            foreach (var rootObj in scene.GetRootGameObjects()) {
                ProcessTree(rootObj);
            }
        }

        private static void ProcessTree(VFGameObject obj) {
            if (obj == null) return;
            if (IsAv3EmulatorClone(obj)) return;

            var avatar = obj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                ProcessOnStartComponent.Process(obj, () => ProcessAvatar(obj));
                return;
            }

            var socket = obj.GetComponent<VRCFuryHapticSocket>();
            if (socket != null) {
                ProcessOnStartComponent.Process(obj, () => {
                    if (socket != null) ProcessSocket(socket);
                });
                return;
            }

            var plug = obj.GetComponent<VRCFuryHapticPlug>();
            if (plug != null) {
                ProcessOnStartComponent.Process(obj, () => {
                    if (plug != null) ProcessPlug(plug);
                });
                return;
            }

            foreach (var child in obj.Children()) {
                ProcessTree(child);
            }
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

        private static void ProcessSocket(VRCFuryHapticSocket socket) {
            socket.Upgrade();
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                try {
                    var bakeResult = VRCFuryHapticSocketEditor.Bake(socket);
                    VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake detached SPS Socket: {socket.owner().GetPath()}", e);
                }
            });
            Object.DestroyImmediate(socket);
        }

        private static void ProcessPlug(VRCFuryHapticPlug plug) {
            plug.Upgrade();
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                try {
                    var bakeResult = VRCFuryHapticPlugEditor.Bake(plug);
                    if (bakeResult != null) {
                        var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", bakeResult.oscId);
                        var saver = new SaveAssetsSession();
                        foreach (var renderer in bakeResult.renderers) {
                            saver.SaveUnsavedComponentAssets(renderer.renderer, tmpDir);
                        }
                        VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake detached SPS Plug: {plug.owner().GetPath()}", e);
                }
            });
            Object.DestroyImmediate(plug);
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
