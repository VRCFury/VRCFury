using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Hooks;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF {
    internal class PlayModeTrigger {
        private const string TriggerObjectName = "__vrcf_play_mode_trigger";
        private static bool scannedThisFrame = false;

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            VRCFuryComponent._OnValidate = () => {
                if (Application.isPlaying && !addedTriggerObjectThisPlayMode && PlayModeMenuItem.Get()) {
                    addedTriggerObjectThisPlayMode = true;
                    var obj = new GameObject(TriggerObjectName);
                    RescanOnStartComponent.AddToObject(obj, true);
                }
            };
            Scheduler.Schedule(() => scannedThisFrame = false, 0);
        }

        private static bool addedTriggerObjectThisPlayMode = false;
        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                addedTriggerObjectThisPlayMode = false;
                TmpFilePackage.Cleanup();
            }
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

        private static void Rescan() {
            if (!Application.isPlaying) return;
            if (!PlayModeMenuItem.Get()) return;
            if (scannedThisFrame) return;
            scannedThisFrame = true;

            foreach (var root in VFGameObject.GetRoots()) {
                foreach (var avatar in root.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
                    if (avatar == null) continue; // it was deleted
                    RescanOnStartComponent.AddToObject(avatar.owner());
                    var obj = avatar.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    if (!RunPreprocessorsOnlyOncePatch.ShouldStartPreprocessors(obj)) continue;
                    if (!VRCFuryBuilder.ShouldRun(obj)) continue;

                    var orig = obj.Clone();
                    orig.name = obj.name;
                    obj.name += "(Clone)";
                    VRCBuildPipelineCallbacks.OnPreprocessAvatar(obj);
                    obj.name = orig.name;
                    orig.Destroy();
                }
                if (root == null) continue; // it was deleted
                foreach (var socket in root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                    RescanOnStartComponent.AddToObject(socket.owner());
                    var obj = socket.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    socket.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() => {
                        try {
                            var hapticContactsService = new HapticContactsService();
                            var bakeResult = VRCFuryHapticSocketEditor.Bake(socket, hapticContactsService);
                            HideAnnoyingGizmosService.Hide(bakeResult.bakeRoot);
                        } catch (Exception e) {
                            throw new ExceptionWithCause($"Failed to bake detached SPS Socket: {socket.owner().GetPath()}", e);
                        }
                    });
                    Object.DestroyImmediate(socket);
                }
                foreach (var plug in root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                    RescanOnStartComponent.AddToObject(plug.owner());
                    var obj = plug.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    plug.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() => {
                        try {
                            var hapticContactsService = new HapticContactsService();
                            var bakeResult = VRCFuryHapticPlugEditor.Bake(plug, hapticContactsService);
                            if (bakeResult != null) {
                                var tmpDir = VRCFuryAssetDatabase.GetUniquePath(TmpFilePackage.GetPath() + "/Builds", bakeResult.name);
                                foreach (var renderer in bakeResult.renderers) {
                                    SaveAssetsService.SaveUnsavedComponentAssets(renderer.renderer, tmpDir);
                                }
                                HideAnnoyingGizmosService.Hide(bakeResult.bakeRoot);
                            }
                        } catch (Exception e) {
                            throw new ExceptionWithCause($"Failed to bake detached SPS Plug: {plug.owner().GetPath()}", e);
                        }
                    });
                    Object.DestroyImmediate(plug);
                }
            }
        }

        public static bool IsAv3EmulatorClone(VFGameObject obj) {
            return obj.name.Contains("(ShadowClone)")
                   || obj.name.Contains("(MirrorReflection)");
        }
        
        private static bool IsWithinAv3EmulatorClone(VFGameObject obj) {
            return obj.GetSelfAndAllParents().Any(IsAv3EmulatorClone);
        }

        [DefaultExecutionOrder(-10000)]
        public class RescanOnStartComponent : MonoBehaviour {
            private void Start() {
                Rescan();
                var obj = gameObject;
                DestroyImmediate(this);
                if (obj.name == TriggerObjectName) {
                    DestroyImmediate(obj);
                }
            }

            public static void AddToObject(VFGameObject obj, bool evenIfAlreadyEnabled = false) {
                if (!Application.isPlaying) return;
                if (obj.GetComponent<RescanOnStartComponent>() != null) return;
                if (!evenIfAlreadyEnabled && obj.activeInHierarchy) return;
                obj.AddComponent<RescanOnStartComponent>();
            }
        }
    }
}
