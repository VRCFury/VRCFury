using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;

namespace VF.Menu {
    public class BulkUpgradeUtils {
        public static void WithAllScenesOpen(Action fn) {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var unloadScenes = new HashSet<Scene>();
            var removeScenes = new HashSet<Scene>();

            try {
                var scenePaths = AssetDatabase.GetAllAssetPaths()
                    .Where(path => typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path))
                    .ToList();
                foreach (var path in scenePaths) {
                    var handled = false;
                    foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount)
                                 .Select(SceneManager.GetSceneAt)) {
                        if (scene.path != path) continue;
                        handled = true;
                        if (!scene.isLoaded) {
                            unloadScenes.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                        }
                    }
                    if (!handled) {
                        removeScenes.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                    }
                }

                fn();
                
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            } finally {
                foreach (var s in unloadScenes) {
                    EditorSceneManager.CloseScene(s, false);
                }
                foreach (var s in removeScenes) {
                    EditorSceneManager.CloseScene(s, true);
                }
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }

        public static (Dictionary<(Transform,Transform), List<T>>, Dictionary<T, string>) FindAll<T>(Func<T, Transform> GetTarget) where T : UnityEngine.Component {
            var map = new Dictionary<(Transform,Transform), List<T>>();
            var sources = new Dictionary<T, string>();

            foreach (var c in FindAll<T>()) {
                if (c == null) continue;
                var target = GetTarget(c);
                var key = (c.transform, target);
                if (!map.ContainsKey(key)) map[key] = new List<T>();
                if (map[key].Contains(c)) continue;
                map[key].Add(c);
                sources[c] = AssetDatabase.GetAssetPath(c);
            }

            return (map, sources);
        }
        
        public static HashSet<T> FindAll<T>() where T : UnityEngine.Component {
            var list = new HashSet<T>();

            foreach (var c in VFGameObject.GetRoots().SelectMany(obj => obj.GetComponentsInSelfAndChildren<T>())) {
                if (c != null) list.Add(c);
            }

            foreach (var path in AssetDatabase.GetAllAssetPaths()) {
                if (typeof(SceneAsset) == AssetDatabase.GetMainAssetTypeAtPath(path)) continue;
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path)) {
                    if (c is T t) {
                        list.Add(t);
                    }
                }
            }

            return list;
        }
    }
}