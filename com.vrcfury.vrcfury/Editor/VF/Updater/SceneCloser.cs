using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;

namespace VF.Updater {
    /**
     * There are two reasons why we have to close all the scenes and then reopen them later.
     * 1. If you delete a prefab while it's open in a scene, unity occasionally throws a "MemoryStream file is corrupted"
     *    error and then crashes.
     * 2. If a serialized object is open in a scene, and then the class backing that object is swapped out with a different
     *    class, unity will show it as "null" until the scene is reloaded. This is especially common for fields that we
     *    change from a non-guid type to a guid type, like AnimationClip to GuidAnimationClip.
     */
    [InitializeOnLoad]
    public static class SceneCloser {
        static SceneCloser() {
            Task.Run(ReopenScenes);
        }

        private static IEnumerable<Scene> GetScenes() {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .ToList();
        }

        private const string UpdateScenePath = "Assets/VRCFury is Updating.unity";

        // TODO: Use this when prefabs update
        public static async Task CloseScenes() {
            await AsyncUtils.InMainThread(() => {
                foreach (var scene in GetScenes()) {
                    if (string.IsNullOrEmpty(scene.path)) {
                        EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath("Assets/Scene.unity"));
                    }
                }

                EditorSceneManager.SaveOpenScenes();
                
                var openPaths = new List<string>();
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.path != UpdateScenePath) openPaths.Add(activeScene.path);
                openPaths.AddRange(GetScenes()
                    .Where(s => s != activeScene && s.path != UpdateScenePath && s.isLoaded)
                    .Select(s => s.path));

                if (openPaths.Count == 0) {
                    return;
                }
                
                Debug.Log("VRCFury is closing loaded scenes");

                var updateScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                foreach (var scene in GetScenes()) {
                    if (scene.path == UpdateScenePath) {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }

                AssetDatabase.DeleteAsset(UpdateScenePath);
                EditorSceneManager.SaveScene(updateScene, UpdateScenePath);

                var info = new string[] {
                    "This is used to re-open your",
                    "scenes after VRCFury has updated.",
                    "If the update fails for some reason,",
                    "you can safely remove this scene.",
                };
                foreach (var line in info) {
                    SceneManager.MoveGameObjectToScene(new GameObject(line), updateScene);
                }
                foreach (var path in openPaths) {
                    SceneManager.MoveGameObjectToScene(new GameObject(path), updateScene);
                }

                foreach (var scene in GetScenes()) {
                    if (scene != updateScene) {
                        EditorSceneManager.CloseScene(scene, false);
                    }
                }
            });
        }

        public static async Task ReopenScenes() {
            await AsyncUtils.InMainThread(() => {
                var updateScene = SceneManager.GetSceneByPath(UpdateScenePath);
                if (!updateScene.IsValid() || !updateScene.isLoaded) return;

                Debug.Log("VRCFury is re-opening loaded scenes");
                var paths = VFGameObject.GetRoots(updateScene)
                    .Select(obj => obj.name)
                    .ToList();
                var first = true;
                foreach (var path in paths.Where(File.Exists)) {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (scene.IsValid() && scene.isLoaded && first) {
                        first = false;
                        EditorSceneManager.SetActiveScene(scene);
                    }
                }

                if (first == false) {
                    EditorSceneManager.CloseScene(updateScene, true);
                    AssetDatabase.DeleteAsset(UpdateScenePath);
                }
                Debug.Log("Reopened");
            });
        }
    }
}
