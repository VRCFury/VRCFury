using System;
using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Menu;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;

namespace VF.Hooks.UdonCleaner {
    /**
     * Some udon changes sneak through our patches. If this happens, we can clean them up just before saving out the assets.
     */
    internal sealed class UdonAssetSaveCleanerHook : AssetModificationProcessor {

        private abstract class Reflection : ReflectionHelper {
            public static readonly System.Reflection.FieldInfo UdonProgramAssetSerializedUdonProgramAssetField = typeof(UdonProgramAsset).VFField("serializedUdonProgramAsset");
            public static readonly System.Reflection.FieldInfo UdonBehaviourSerializedProgramAssetField = typeof(UdonBehaviour).VFField("serializedProgramAsset");
        }

        private static string[] OnWillSaveAssets(string[] paths) {
            if (!SimplifyUdonSerializationMenuItem.Get()) return paths;
            if (!ReflectionHelper.IsReady<Reflection>()) return paths;
            foreach (var path in paths) {
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var obj in EnumerateSavedObjects(path)) {
                    ClearGeneratedProgramReferences(obj);
                }
            }

            return paths;
        }

        private static IEnumerable<UnityEngine.Object> EnumerateSavedObjects(string path) {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) {
                for (var i = 0; i < SceneManager.sceneCount; i++) {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || scene.path != path) continue;

                    foreach (var root in scene.GetRootGameObjects()) {
                        foreach (var obj in EnumerateGameObjectTree(root)) {
                            yield return obj;
                        }
                    }
                }

                yield break;
            }

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null &&
                    string.Equals(prefabStage.assetPath, path, StringComparison.OrdinalIgnoreCase) &&
                    prefabStage.prefabContentsRoot != null) {
                    foreach (var obj in EnumerateGameObjectTree(prefabStage.prefabContentsRoot)) {
                        yield return obj;
                    }

                    yield break;
                }

                var prefabAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (prefabAsset is GameObject prefabRoot) {
                    foreach (var obj in EnumerateGameObjectTree(prefabRoot)) {
                        yield return obj;
                    }
                }

                yield break;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                if (asset != null) yield return asset;
            }
        }

        private static IEnumerable<UnityEngine.Object> EnumerateGameObjectTree(GameObject root) {
            if (root == null) yield break;

            ClearGeneratedPrefabInstanceModifications(root);

            foreach (var behaviour in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) {
                yield return behaviour;
            }

            foreach (var behaviour in root.GetComponentsInChildren<UdonBehaviour>(true)) {
                yield return behaviour;
            }
        }

        private static void ClearGeneratedPrefabInstanceModifications(GameObject root) {
            var processedRoots = new HashSet<GameObject>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) {
                if (transform == null) continue;
                var gameObject = transform.gameObject;
                if (!PrefabUtility.IsPartOfPrefabInstance(gameObject)) continue;

                var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                if (prefabInstanceRoot == null || !processedRoots.Add(prefabInstanceRoot)) continue;
                ClearGeneratedPrefabInstanceModificationsOnRoot(prefabInstanceRoot);
            }
        }

        private static void ClearGeneratedPrefabInstanceModificationsOnRoot(GameObject prefabInstanceRoot) {
            var modifications = PrefabUtility.GetPropertyModifications(prefabInstanceRoot);
            if (modifications == null || modifications.Length == 0) return;

            var keptModifications = new List<PropertyModification>(modifications.Length);
            var changed = false;
            foreach (var modification in modifications) {
                if (ShouldRemoveGeneratedProgramModification(modification)) {
                    changed = true;
                    continue;
                }

                keptModifications.Add(modification);
            }

            if (!changed) return;
            PrefabUtility.SetPropertyModifications(prefabInstanceRoot, keptModifications.ToArray());
        }

        private static bool ShouldRemoveGeneratedProgramModification(PropertyModification modification) {
            if (modification == null) return false;

            var isUnresolved = modification.target == null;
            var isUdonBehaviour = false;
            var isUdonSharpBacker = false;
            var isUdonSharpBehaviour = modification.target is UdonSharpBehaviour;
            if (modification.target is UdonBehaviour ub) {
                isUdonBehaviour = true;
                isUdonSharpBacker = UdonSharpEditorUtility.IsUdonSharpBehaviour(ub);
            }

            if (modification.propertyPath == "_syncMethod") {
                return isUnresolved || isUdonSharpBacker;
            }

            if (modification.propertyPath == "serializedProgramAsset") {
                return isUnresolved || isUdonBehaviour;
            }

            if (modification.propertyPath == "_udonSharpBackingUdonBehaviour") {
                return isUnresolved;
            }

            if (modification.propertyPath == "serializationData.Prefab") {
                return isUnresolved || isUdonSharpBacker || isUdonSharpBehaviour;
            }

            return false;
        }

        private static void ClearGeneratedProgramReferences(UnityEngine.Object target) {
            if (target is UdonProgramAsset) {
                ClearObjectReferenceProperty(target, Reflection.UdonProgramAssetSerializedUdonProgramAssetField);
            }

            if (target is UdonBehaviour) {
                ClearObjectReferenceProperty(target, Reflection.UdonBehaviourSerializedProgramAssetField);
            }
        }

        private static bool ClearObjectReferenceProperty(UnityEngine.Object target, System.Reflection.FieldInfo field) {
            if (target == null) return false;
            if (PrefabUtility.IsPartOfPrefabInstance(target)) {
                // This will be handled in the prefab modifications pass later
                return false;
            }

            field.SetValue(target, null);
            EditorUtility.SetDirty(target);
            return true;
        }
    }
}
