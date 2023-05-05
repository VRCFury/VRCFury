using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder.Exceptions;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Menu {
    public class DuplicatePhysboneDetector {
        [MenuItem(MenuItems.detectDuplicatePhysbones, priority = MenuItems.detectDuplicatePhysbonesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(RunUnsafe);
        }
        
        private static void RunUnsafe() {
            var start = EditorUtility.DisplayDialog(
                "Duplicate Physbones",
                "This tool will load every scene and prefab, and check for any bones that have more than one physbone targeting them (which is usually bad). This may take a lot of ram and time. Continue?",
                "Yes",
                "Cancel"
            );
            if (!start) return;
            
            WithAllScenesOpen(() => {
                var bad = new List<string>();
                FindDupes<VRCPhysBone>(c => c.GetRootTransform(), bad);

                if (bad.Count == 0) {
                    EditorUtility.DisplayDialog(
                        "Duplicate Physbones",
                        "No duplicates found in loaded objects.",
                        "Ok"
                    );
                    return;
                }

                var split = string.Join("\n\n", bad).Split('\n').ToList();
                while (split.Count > 0) {
                    var numToPick = Math.Min(split.Count, 40);
                    var part = split.GetRange(0, numToPick);
                    split.RemoveRange(0, numToPick);
                    
                    var message = "Duplicate physbones found in loaded objects:\n\n" + string.Join("\n", part);
                    if (split.Count > 0) message += "\n... and more (will be shown in next dialog)";
                    
                    message += "\n\nDelete the duplicates?";
                    var ok = EditorUtility.DisplayDialog(
                        "Duplicate Physbones",
                        message,
                        "Ok, Delete Duplicates",
                        "Cancel"
                    );
                    if (!ok) return;
                }

                FixDupes<VRCPhysBone>(c => c.GetRootTransform());
            });
        }

        private static void WithAllScenesOpen(Action fn) {
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
                        if (scene.path == path) {
                            handled = true;
                            if (!scene.isLoaded) {
                                unloadScenes.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                            }
                        }
                    }
                    if (!handled) {
                        removeScenes.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                    }
                }

                fn();
            } finally {
                foreach (var s in unloadScenes) {
                    EditorSceneManager.CloseScene(s, false);
                }
                foreach (var s in removeScenes) {
                    EditorSceneManager.CloseScene(s, true);
                }
            }
        }
        
        private static (Dictionary<(Transform,Transform), List<T>>, Dictionary<T, string>) FindAll<T>(Func<T, Transform> GetTarget) where T : UnityEngine.Component {
            var map = new Dictionary<(Transform,Transform), List<T>>();
            var sources = new Dictionary<T, string>();

            void AddOne(T c) {
                if (c == null) return;
                var target = GetTarget(c);
                var key = (c.transform, target);
                if (!map.ContainsKey(key)) map[key] = new List<T>();
                if (map[key].Contains(c)) return;
                map[key].Add(c);
            }

            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt)) {
                if (scene.isLoaded) {
                    foreach (var c in scene.GetRootGameObjects()
                                 .SelectMany(obj => obj.GetComponentsInChildren<T>(true))) {
                        AddOne(c);
                        sources[c] = scene.path;
                    }
                }
            }

            foreach (var path in AssetDatabase.GetAllAssetPaths()) {
                if (typeof(SceneAsset) != AssetDatabase.GetMainAssetTypeAtPath(path)) {
                    foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path)) {
                        if (c is T t) {
                            AddOne(t);
                            sources[t] = path;
                        }
                    }
                }
            }
            return (map, sources);
        }

        private static string GetName(Transform t) {
            return t.root.name + "/" + AnimationUtility.CalculateTransformPath(t, t.root)
                   + " (" + AssetDatabase.GetAssetPath(t) + ")";
        }
        private static string GetName<T>(T c, Dictionary<T, string> sources) where T : UnityEngine.Component {
            return c.transform.root.name + "/" + AnimationUtility.CalculateTransformPath(c.transform, c.transform.root)
                   + " (" + sources[c] + ")"
                   + (IsMutable(c) ? "" : " (Immutable)");
        }

        private static void FindDupes<T>(Func<T, Transform> GetTarget, List<string> badList) where T : UnityEngine.Component {
            var (map, sources) = FindAll(GetTarget);
            foreach (var ((transform,target),components) in map.Select(x => (x.Key, x.Value))) {
                if (components.Count == 1) continue;
                var mutable = components.Where(IsMutable).ToList();
                if (mutable.Count == 0) continue;
                var targetStr = GetName(target);
                var cStrs = string.Join("\n", components.Select(c => GetName(c, sources)));
                badList.Add( $"{targetStr}\nis targeted by\n{cStrs}");
            }
        }

        private static bool IsMutable(UnityEngine.Component c) {
            return !PrefabUtility.IsPartOfImmutablePrefab(c) && !PrefabUtility.IsPartOfPrefabInstance(c);
        }
        
        private static void FixDupes<T>(Func<T, Transform> GetTarget) where T : UnityEngine.Component {
            var (map, sources) = FindAll(GetTarget);
            foreach (var ((transform,target),components) in map.Select(x => (x.Key, x.Value))) {
                if (components.Count == 1) continue;
                var mutable = components.Where(IsMutable).ToList();
                if (mutable.Count == components.Count) {
                    mutable.RemoveAt(0);
                }

                foreach (var c in mutable) {
                    var obj = c.gameObject;
                    Object.DestroyImmediate(c, true);
                    EditorUtility.SetDirty(obj);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
