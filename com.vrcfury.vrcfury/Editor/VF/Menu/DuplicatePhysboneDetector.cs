using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
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

            var message = "Duplicate physbones found in loaded objects:\n\n" + string.Join("\n\n", bad);

            var split = message.Split('\n');
            if (split.Length > 80) {
                message = string.Join("\n", new ArraySegment<string>(split, 0, 80));
                message += "\n... and more";
            }
            message += "\n\nDelete the duplicates?";
            
            var ok = EditorUtility.DisplayDialog(
                "Duplicate Physbones",
                message,
                "Ok, Delete Duplicates",
                "Cancel"
            );
            if (!ok) return;

            FixDupes<VRCPhysBone>(c => c.GetRootTransform());
        }
        
        private static Dictionary<(Transform,Transform), List<T>> FindAll<T>(Func<T, Transform> GetTarget) where T : Component {
            var map = new Dictionary<(Transform,Transform), List<T>>();

            void AddOne(T c) {
                var target = GetTarget(c);
                var key = (c.transform, target);
                if (!map.ContainsKey(key)) map[key] = new List<T>();
                if (map[key].Contains(c)) return;
                map[key].Add(c);
            }

            foreach (var c in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt)
                         .Where(scene => scene.isLoaded)
                         .SelectMany(scene => scene.GetRootGameObjects())
                         .SelectMany(obj => obj.GetComponentsInChildren<T>(true))) {
                AddOne(c);
            }
            foreach (var path in AssetDatabase.GetAllAssetPaths()) {
                if (path.EndsWith(".asset") || path.EndsWith(".prefab")) {
                    foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path)) {
                        if (c is T t) {
                            AddOne(t);
                        }
                    }
                }
            }
            foreach (var c in Resources.FindObjectsOfTypeAll<T>()) {
                AddOne(c);
            }
            return map;
        }

        private static string GetName(Transform t) {
            return t.root.name + "/" + AnimationUtility.CalculateTransformPath(t, t.root) + " (" +
                   AssetDatabase.GetAssetPath(t) + ")";
        }
        private static string GetName(Component c) {
            return GetName(c.transform) + (IsMutable(c) ? "" : " (Immutable)");
        }

        private static void FindDupes<T>(Func<T, Transform> GetTarget, List<string> badList) where T : Component {
            var map = FindAll(GetTarget);
            foreach (var ((transform,target),components) in map.Select(x => (x.Key, x.Value))) {
                if (components.Count == 1) continue;
                var mutable = components.Where(IsMutable).ToList();
                if (mutable.Count == 0) continue;
                var targetStr = GetName(target);
                var cStrs = string.Join("\n", components.Select(GetName));
                badList.Add( $"{targetStr}\nis targeted by\n{cStrs}");
            }
        }

        private static bool IsMutable(Component c) {
            return !PrefabUtility.IsPartOfImmutablePrefab(c) && !PrefabUtility.IsPartOfPrefabInstance(c);
        }
        
        private static void FixDupes<T>(Func<T, Transform> GetTarget) where T : Component {
            var map = FindAll(GetTarget);
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
