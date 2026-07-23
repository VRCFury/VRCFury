using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
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
                using (new VRCFuryBuildContext()) {
                    TmpDirService.Cleanup();
                }
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
            var spsMarkers = new SpsMarkersService();
            foreach (var rootObj in scene.GetRootGameObjects()) {
                ProcessTree(rootObj, spsMarkers);
            }
        }

        private static void ProcessTree(VFGameObject obj, SpsMarkersService spsMarkers) {
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
                    if (socket != null) ProcessSocket(socket, spsMarkers);
                });
                return;
            }

            var plug = obj.GetComponent<VRCFuryHapticPlug>();
            if (plug != null) {
                ProcessOnStartComponent.Process(obj, () => {
                    if (plug != null) ProcessPlug(plug, spsMarkers);
                });
                return;
            }

            foreach (var child in obj.Children()) {
                ProcessTree(child, spsMarkers);
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

        private static void ProcessSocket(VRCFuryHapticSocket socket, SpsMarkersService spsMarkers) {
            using (new VRCFuryBuildContext()) {
                socket.Upgrade();
                VRCFExceptionUtils.ErrorDialogBoundary(() => {
                    try {
                        var bakeResult = VRCFuryHapticSocketEditor.Bake(socket, spsMarkers);
                        if (bakeResult != null) {
                            var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", "Socket mat");
                            SpsConfigurer.AddMaterialPropertyAnimator(
                                bakeResult.screenMarkerResults.SelectMany(result => result.materialProperties),
                                tmpDir
                            );
                            var saver = new SaveAssetsSession();
                            foreach (var c in bakeResult.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            foreach (var c in bakeResult.screenMarkers
                                         .SelectMany(c => c.GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                        }
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to bake detached SPS Socket: {socket.owner().GetPath()}", e);
                    }
                });
            }
            Object.DestroyImmediate(socket);
        }

        private static void ProcessPlug(VRCFuryHapticPlug plug, SpsMarkersService spsMarkers) {
            using (new VRCFuryBuildContext()) {
                plug.Upgrade();
                VRCFExceptionUtils.ErrorDialogBoundary(() => {
                    try {
                        var bakeResult = VRCFuryHapticPlugEditor.Bake(plug, spsMarkers: spsMarkers);
                        if (bakeResult != null) {
                            var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", bakeResult.oscId);
                            if (bakeResult.resolverMaterialProperties != null) {
                                SpsConfigurer.AddMaterialPropertyAnimator(bakeResult.resolverMaterialProperties, tmpDir);
                            }
                            var saver = new SaveAssetsSession();
                            foreach (var c in bakeResult.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            foreach (var c in bakeResult.renderers.SelectMany(r => r.renderer.owner().GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                                saver.SaveUnsavedComponentAssets(c, tmpDir);
                            }
                            VRCFuryHideGizmoUnlessSelectedExtensions.Hide(bakeResult.bakeRoot);
                        }
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to bake detached SPS Plug: {plug.owner().GetPath()}", e);
                    }
                });
            }
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
