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
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF {
    [InitializeOnLoad]
    public class PlayModeTrigger {
        private static string tmpDir;

        static PlayModeTrigger() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                if (PlayModeMenuItem.Get()) {
                    var rootObjects = VFGameObject.GetRoots();
                    VRCFPrefabFixer.Fix(rootObjects);
                }
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
                if (PrefabUtility.IsPartOfAnyPrefab(t)) {
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

            var builder = new VRCFuryBuilder();
            var restartAudioLink = false;
            var restartAv3Emulator = false;
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
                    if (builder.SafeRun(obj)) {
                        VRCFuryBuilder.StripAllVrcfComponents(obj);
                        restartAudioLink = true;
                        if (obj.GetComponents<UnityEngine.Component>().Any(c => c.GetType().Name == "LyumaAv3Runtime")) {
                            restartAv3Emulator = true;
                        }
                    } else {
                        var name = obj.name;
                        var failMarker = new GameObject(name + " (VRCFury Failed)");
                        SceneManager.MoveGameObjectToScene(failMarker, obj.scene);
                        Object.DestroyImmediate(obj);
                    }
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
                        VRCFuryHapticSocketEditor.Bake(socket, onlySenders: true);
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
                        var mutableManager = new MutableManager(tmpDir);
                        VRCFuryHapticPlugEditor.Bake(plug, onlySenders: true, mutableManager: mutableManager);
                    });
                    Object.DestroyImmediate(plug);
                }
            }

            if (restartAv3Emulator) {
                EditorApplication.delayCall += RestartAv3Emulator;
            }
            if (restartAudioLink) {
                RestartAudiolink();
            }
        }

        private static bool IsAv3EmulatorClone(VFGameObject obj) {
            return obj.name.Contains("(ShadowClone)")
                   || obj.name.Contains("(MirrorReflection)");
        }
        
        private static bool IsWithinAv3EmulatorClone(VFGameObject obj) {
            return obj.GetSelfAndAllParents().Any(IsAv3EmulatorClone);
        }

        private static void DestroyAllOfType(string typeStr) {
            var type = ReflectionUtils.GetTypeFromAnyAssembly(typeStr);
            if (type == null) return;
            foreach (var runtime in Object.FindObjectsOfType(type)) {
                Object.DestroyImmediate(runtime);
            }
        }

        private static void ClearField(object obj, string fieldStr) {
            var field = obj.GetType().GetField(fieldStr);
            if (field == null) return;
            var value = field.GetValue(obj);
            if (value == null) return;
            var clear = value.GetType().GetMethod("Clear");
            if (clear == null) return;
            clear.Invoke(value, new object[]{});
        }

        /**
         * This is needed because (at least at this time), Av3Emulator hooks during Awake, which is before we have a chance to build.
         * If some day Av3Emulator moves to Start(), this will automatically stop being used, since we only call it if LyumaAv3Runtime
         * exists on the avatar when we build.
         */
        private static void RestartAv3Emulator() {
            try {
                var av3EmulatorType = ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Emulator");
                if (av3EmulatorType == null) av3EmulatorType = ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Emulator");
                if (av3EmulatorType == null) return;
                
                Debug.Log("Restarting av3emulator ...");
                
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime");
                DestroyAllOfType("LyumaAv3Runtime");
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.LyumaAv3Menu");
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.GestureManagerAv3Menu");

                var restartField = av3EmulatorType.GetField("RestartEmulator");
                if (restartField == null) throw new Exception("Failed to find RestartEmulator field");
                var emulators = Object.FindObjectsOfType(av3EmulatorType);
                foreach (var emulator in emulators) {
                    ClearField(emulator, "runtimes");
                    ClearField(emulator, "forceActiveRuntimes");
                    ClearField(emulator, "scannedAvatars");
                    restartField.SetValue(emulator, true);
                }

                foreach (var root in VFGameObject.GetRoots()) {
                    if (IsAv3EmulatorClone(root)) {
                        root.Destroy();
                    }
                }
            } catch (Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
                    "VRCFury",
                    "VRCFury detected Av3Emulator, but was unable to reload it after making changes to the avatar." +
                    " Because of this, testing with the emulator may not be correct." +
                    " Report this on https://vrcfury.com/discord\n\n" + e.Message,
                    "Ok"
                );
            }
        }

        /**
         * This is needed because AudioLink sets a texture on all materials globally. If we introduce a new material, we need
         * it to set that texture again.
         */
        private static void RestartAudiolink() {
            var alComponentType = ReflectionUtils.GetTypeFromAnyAssembly("VRCAudioLink.AudioLink");
            if (alComponentType == null) alComponentType = ReflectionUtils.GetTypeFromAnyAssembly("AudioLink.AudioLink");
            if (alComponentType == null) return;
            foreach (var gm in Object.FindObjectsOfType(alComponentType).OfType<UnityEngine.Component>()) {
                Debug.Log("Restarting AudioLink ...");
                if (gm.gameObject.activeSelf) {
                    gm.gameObject.SetActive(false);
                    gm.gameObject.SetActive(true);
                }
            }
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
