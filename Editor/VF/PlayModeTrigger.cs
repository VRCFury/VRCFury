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
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            Debug.Log("Scene loaded " + scene);
            if (!Application.isPlaying) return;
            if (!PlayModeMenuItem.Get()) return;
            ScanScene(scene);
        }

        // This should absolutely always be false in play mode, but we check just in case
        private static bool ContainsAnyPrefabs(GameObject obj) {
            foreach (var t in obj.GetComponentsInChildren<Transform>()) {
                if (PrefabUtility.IsPartOfAnyPrefab(t.gameObject)) {
                    return true;
                }
            }
            return false;
        }

        private static void ScanScene(Scene scene) {
            var builder = new VRCFuryBuilder();
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>()) {
                    if (ContainsAnyPrefabs(avatar.gameObject)) continue;
                    if (avatar.gameObject.name.Contains("(ShadowClone)") ||
                        avatar.gameObject.name.Contains("(MirrorReflection)")) {
                        // these are av3emulator temp objects. Building on them doesn't work.
                        continue;
                    }
                    builder.SafeRun(avatar.gameObject);
                    VRCFuryBuilder.StripAllVrcfComponents(avatar.gameObject);
                }
                foreach (var o in root.GetComponentsInChildren<OGBOrifice>()) {
                    if (ContainsAnyPrefabs(o.gameObject)) continue;
                    OGBOrificeEditor.Bake(o, onlySenders: true);
                    Object.DestroyImmediate(o);
                }
                foreach (var o in root.GetComponentsInChildren<OGBPenetrator>()) {
                    if (ContainsAnyPrefabs(o.gameObject)) continue;
                    OGBPenetratorEditor.Bake(o, onlySenders: true);
                    Object.DestroyImmediate(o);
                }
            }
        }
    }
}