using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.PlayMode;
using VF.Service;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF {
    public class PlayModeTrigger {
        private static string tmpDir;

        [InitializeOnLoadMethod]
        static void Init() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                tmpDir = null;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (VrcIsUploadingDetector.IsProbablyUploading()) return;
            if (!EditorApplication.isPlaying) return;
            Rescan(scene);
        }

        // This should absolutely always be false in play mode, but we check just in case
        private static bool ContainsAnyPrefabs(VFGameObject obj) {
            foreach (var t in obj.GetSelfAndAllChildren()) {
                // Don't use IsPartOfAnyPrefab because for some reason it randomly returns
                // true in play mode on 2019, even if every other "IsPartOf..." returns false.
                if (PrefabUtility.IsPartOfPrefabInstance(t)) {
                    Debug.LogWarning(
                        "VRCF is not running on " +
                        obj.GetPath() +
                        " while in play mode because it somehow contains a prefab instance");
                    return true;
                }
            }
            return false;
        }

        private static void Rescan(Scene scene) {
            if (!Application.isPlaying) return;
            if (!PlayModeMenuItem.Get()) return;

            if (tmpDir == null) {
                var tmpDirParent = TmpFilePackage.GetPath() + "/PlayMode";
                VRCFuryAssetDatabase.DeleteFolder(tmpDirParent);
                tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";
                Directory.CreateDirectory(tmpDir);
            }

            foreach (var root in VFGameObject.GetRoots(scene)) {
                foreach (var avatar in root.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
                    RescanOnStartComponent.AddToObject(avatar.gameObject);
                    var obj = avatar.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    if (obj.GetComponentsInSelfAndChildren<VRCFuryTest>().Length > 0) {
                        continue;
                    }
                    if (!VRCFuryBuilder.ShouldRun(obj)) continue;

                    var orig = obj.Clone();
                    orig.name = obj.name;
                    obj.name += "(Clone)";
                    VRCBuildPipelineCallbacks.OnPreprocessAvatar(obj);
                    obj.name = orig.name;
                    orig.Destroy();
                }
                if (root.gameObject == null) continue; // it was deleted
                foreach (var socket in root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                    RescanOnStartComponent.AddToObject(socket.gameObject);
                    var obj = socket.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    socket.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() => {
                        try {
                            var hapticContactsService = new HapticContactsService();
                            VRCFuryHapticSocketEditor.Bake(socket, hapticContactsService);
                        } catch (Exception e) {
                            throw new ExceptionWithCause($"Failed to bake detached SPS Socket: {socket.owner().GetPath()}", e);
                        }
                    });
                    Object.DestroyImmediate(socket);
                }
                foreach (var plug in root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                    RescanOnStartComponent.AddToObject(plug.gameObject);
                    var obj = plug.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    plug.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() => {
                        try {
                            var hapticContactsService = new HapticContactsService();
                            VRCFuryHapticPlugEditor.Bake(plug, hapticContactsService, tmpDir);
                        } catch (Exception e) {
                            throw new ExceptionWithCause($"Failed to bake detached SPS Plug: {plug.owner().GetPath()}", e);
                        }
                    });
                    Object.DestroyImmediate(plug);
                }
            }
        }

        private static bool IsAv3EmulatorClone(VFGameObject obj) {
            return obj.name.Contains("(ShadowClone)")
                   || obj.name.Contains("(MirrorReflection)");
        }
        
        private static bool IsWithinAv3EmulatorClone(VFGameObject obj) {
            return obj.GetSelfAndAllParents().Any(IsAv3EmulatorClone);
        }

        [DefaultExecutionOrder(-10000)]
        public class RescanOnStartComponent : MonoBehaviour {
            private void Start() {
                Rescan(gameObject.scene);
            }

            public static void AddToObject(GameObject obj) {
                if (obj.GetComponent<RescanOnStartComponent>()) {
                    return;
                }
                obj.AddComponent<RescanOnStartComponent>();
            }
        }
    }
}
