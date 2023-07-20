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
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF {
    [InitializeOnLoad]
    public class PlayModeTrigger : IVRCSDKPostprocessAvatarCallback {
        private static double lastRescan = 0;
        private static string AboutToUploadKey = "vrcf_vrcAboutToUpload";
        private static string tmpDir;
        public int callbackOrder => int.MaxValue;
        public void OnPostprocessAvatar() {
            EditorPrefs.SetFloat(AboutToUploadKey, Now());
        }

        private static float Now() {
            return (float)EditorApplication.timeSinceStartup;
        }

        private static bool activeNow = false;
        static PlayModeTrigger() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += () => {
                var now = Now();
                if (now > lastRescan + 0.5) {
                    lastRescan = now;
                    Rescan();
                }
            };
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            var aboutToUploadTime = EditorPrefs.GetFloat(AboutToUploadKey, 0);
            var now = Now();
            activeNow = false;
            var problyUploading = aboutToUploadTime <= now && aboutToUploadTime > Now() - 10;
            if (state == PlayModeStateChange.ExitingEditMode) {
                if (!problyUploading && PlayModeMenuItem.Get()) {
                    var rootObjects = VFGameObject.GetRoots();
                    VRCFPrefabFixer.Fix(rootObjects);
                }

                tmpDir = null;
            } else if (state == PlayModeStateChange.EnteredPlayMode) {
                if (problyUploading) {
                    EditorPrefs.DeleteKey(AboutToUploadKey);
                    return;
                }
                EditorPrefs.DeleteKey(AboutToUploadKey);
                activeNow = true;
                Rescan();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            Rescan();
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

        private static void Rescan() {
            if (!Application.isPlaying) return;
            if (!PlayModeMenuItem.Get()) return;
            if (!activeNow) return;

            if (tmpDir == null) {
                var tmpDirParent = TmpFilePackage.GetPath() + "/PlayMode";
                VRCFuryAssetDatabase.DeleteFolder(tmpDirParent);
                tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";
                Directory.CreateDirectory(tmpDir);
            }

            var builder = new VRCFuryBuilder();
            var oneChanged = false;
            foreach (var root in VFGameObject.GetRoots()) {
                foreach (var avatar in root.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
                    var obj = avatar.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    if (obj.GetComponentsInSelfAndChildren<VRCFuryTest>().Length > 0) {
                        continue;
                    }
                    if (!VRCFuryBuilder.ShouldRun(obj)) continue;
                    builder.SafeRun(obj);
                    VRCFuryBuilder.StripAllVrcfComponents(obj);
                    oneChanged = true;
                    RestartPhysbones(obj);
                }
                foreach (var socket in root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                    var obj = socket.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    if (IsWithinAv3EmulatorClone(obj)) continue;
                    socket.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() => {
                        VRCFuryHapticSocketEditor.Bake(socket, onlySenders: true);
                    });
                    Object.DestroyImmediate(socket);
                    RestartPhysbones(obj);
                }
                foreach (var plug in root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
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
                    RestartPhysbones(obj);
                }
            }

            if (oneChanged) {
                RestartVrcsdk();
                RestartAv3Emulator();
                RestartGestureManager();
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

        private static void RestartVrcsdk() {
            try {
                var initClass = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Avatars.AvatarDynamicsSetup");
                if (initClass == null) return;
                var initTrigger = initClass.GetMethod("Trigger_OnInitialize",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (initTrigger == null) return;
                var initPhysBone = initClass.GetMethod("PhysBone_OnInitialize",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (initPhysBone == null) return;
                
                Debug.Log($"Restarting VRCSDK components ...");

                foreach (var receiver in Object.FindObjectsOfType<ContactReceiver>()) {
                    receiver.paramAccess = null;
                    initTrigger.Invoke(null, new object[] { receiver });
                }
                foreach (var physbone in Object.FindObjectsOfType<VRCPhysBoneBase>()) {
                    physbone.param_Angle = null;
                    physbone.param_Stretch = null;
                    physbone.param_IsGrabbed = null;
                    initPhysBone.Invoke(null, new object[] { physbone });
                }
            } catch (Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
                    "VRCFury",
                    "VRCFury was unable to reload the VRCSDK after making changes to the avatar." +
                    " Because of this, testing in play mode may not be 100% correct." +
                    " Report this on https://vrcfury.com/discord\n\n" + e.Message,
                    "Ok"
                );
            }
        }

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

        private static void RestartGestureManager() {
            try {
                var gmType = ReflectionUtils.GetTypeFromAnyAssembly("BlackStartX.GestureManager.GestureManager");
                if (gmType == null) return;
                Debug.Log("Restarting gesture manager ...");
                foreach (var gm in Object.FindObjectsOfType(gmType).OfType<UnityEngine.Component>()) {
                    if (gm.gameObject.activeSelf) {
                        gm.gameObject.SetActive(false);
                        gm.gameObject.SetActive(true);
                    }

                    if (Selection.activeGameObject == gm.gameObject) {
                        Selection.activeGameObject = null;
                        EditorApplication.delayCall += () => Selection.activeGameObject = gm.gameObject;
                    }
                }
            } catch (Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
                    "VRCFury",
                    "VRCFury detected GestureManager, but was unable to reload it after making changes to the avatar." +
                    " Because of this, testing with the emulator may not be correct." +
                    " Report this on https://vrcfury.com/discord\n\n" + e.Message,
                    "Ok"
                );
            }
        }
        
        private static void RestartAudiolink() {
            var alComponentType = ReflectionUtils.GetTypeFromAnyAssembly("VRCAudioLink.AudioLink");
            if (alComponentType == null) return;
            foreach (var gm in Object.FindObjectsOfType(alComponentType).OfType<UnityEngine.Component>()) {
                Debug.Log("Restarting AudioLink ...");
                if (gm.gameObject.activeSelf) {
                    gm.gameObject.SetActive(false);
                    gm.gameObject.SetActive(true);
                }
            }
        }

        private static void RestartPhysbones(VFGameObject obj) {
            foreach (var physbone in obj.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                physbone.InitTransforms(true);
            }
        }
    }
}
