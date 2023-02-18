using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VRC.SDK3.Avatars.Components;

namespace VF {
    [InitializeOnLoad]
    public class PlayModeTrigger {
        static PlayModeTrigger() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) {
                for (var i = 0; i < SceneManager.sceneCount; i++) {
                    OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
                }
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            Debug.Log("Scene loaded " + scene);
            // Delay a frame so VRCSDK has a chance to show up
            EditorApplication.delayCall += () => ScanScene(scene);
        }

        // This should absolutely always be false in play mode, but we check just in case
        private static bool ContainsAnyPrefabs(GameObject obj) {
            foreach (var t in obj.GetComponentsInChildren<Transform>(true)) {
                if (PrefabUtility.IsPartOfAnyPrefab(t.gameObject)) {
                    return true;
                }
            }
            return false;
        }

        private static void ScanScene(Scene scene) {
            if (scene == null) return;
            if (!scene.isLoaded) return;
            if (!Application.isPlaying) return;
            if (!PlayModeMenuItem.Get()) return;

            var uploading = SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(o => o.name == "VRCSDK")
                .Any();
            if (uploading) {
                // We're uploading
                return;
            }

            var builder = new VRCFuryBuilder();
            var oneChanged = false;
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>(true)) {
                    if (ContainsAnyPrefabs(avatar.gameObject)) continue;
                    if (avatar.gameObject.name.Contains("(ShadowClone)") ||
                        avatar.gameObject.name.Contains("(MirrorReflection)")) {
                        // these are av3emulator temp objects. Building on them doesn't work.
                        continue;
                    }
                    if (!VRCFuryBuilder.ShouldRun(avatar.gameObject)) continue;
                    builder.SafeRun(avatar.gameObject);
                    VRCFuryBuilder.StripAllVrcfComponents(avatar.gameObject);
                    oneChanged = true;
                }
                foreach (var o in root.GetComponentsInChildren<OGBOrifice>(true)) {
                    if (ContainsAnyPrefabs(o.gameObject)) continue;
                    OGBOrificeEditor.Bake(o, onlySenders: true);
                    Object.DestroyImmediate(o);
                }
                foreach (var o in root.GetComponentsInChildren<OGBPenetrator>(true)) {
                    if (ContainsAnyPrefabs(o.gameObject)) continue;
                    OGBPenetratorEditor.Bake(o, onlySenders: true);
                    Object.DestroyImmediate(o);
                }
            }

            if (oneChanged) {
                RestartAv3Emulator();
            }
        }

        private static void RestartAv3Emulator() {
            // Restart the av3emulator so it picks up changes
            var av3EmulatorType = ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Emulator");
            if (av3EmulatorType == null) return;
            var restartField = av3EmulatorType.GetField("RestartEmulator");
            if (restartField == null) return;
            var emulators = Object.FindObjectsOfType(av3EmulatorType);
            foreach (var emulator in emulators) {
                restartField.SetValue(emulator, true);
            }
            var av3RuntimeType = ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Runtime");
            var runtimes = Object.FindObjectsOfType(av3RuntimeType);
            foreach (var runtime in runtimes) {
                Object.Destroy(runtime);
            }
            
            // Restart gesture manager
            var gmType = ReflectionUtils.GetTypeFromAnyAssembly("BlackStartX.GestureManager.GestureManager");
            foreach (var gm in Object.FindObjectsOfType(gmType).OfType<Component>()) {
                if (gm.gameObject.activeSelf) {
                    gm.gameObject.SetActive(false);
                    gm.gameObject.SetActive(true);
                }
            }
        }
    }
}