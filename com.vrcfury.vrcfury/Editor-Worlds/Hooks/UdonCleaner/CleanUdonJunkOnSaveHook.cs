using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharp.Updater;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Menu;
using VF.Utils;
using VRC.SDK3.Editor;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;

namespace VF.Hooks.UdonCleaner {
    /**
     * Some udon changes sneak through CleanUdonJunkOnChangeHook.
     * If this happens, we can clean them up just before saving out the assets.
     */
    internal sealed class CleanUdonJunkOnSaveHook : AssetModificationProcessor {
        private abstract class Reflection : ReflectionHelper {
            public static readonly System.Reflection.FieldInfo UdonProgramAssetSerializedUdonProgramAssetField = typeof(UdonProgramAsset).VFField("serializedUdonProgramAsset");
            public static readonly System.Reflection.FieldInfo UdonBehaviourSerializedProgramAssetField = typeof(UdonBehaviour).VFField("serializedProgramAsset");
            public static readonly System.Reflection.FieldInfo programSource = typeof(UdonBehaviour).VFField(nameof(UdonBehaviour.programSource));
        }

        private static string[] OnWillSaveAssets(string[] paths) {
            if (!UdonCleanerMenuItem.Get()) return paths;
            if (!ReflectionHelper.IsReady<Reflection>()) return paths;
            CleanPaths(paths);
            return paths;
        }

        private static void CleanPaths(string[] paths) {
            foreach (var path in paths) {
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var obj in EnumerateSavedObjects(path)) {
                    ClearGeneratedProgramReferences(obj);
                }
            }
        }

        private static IEnumerable<UnityEngine.Object> EnumerateSavedObjects(string path) {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) {
                for (var i = 0; i < SceneManager.sceneCount; i++) {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || scene.path != path) continue;

                    foreach (var root in scene.Roots()) {
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

                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach (var obj in EnumerateGameObjectTree(prefabRoot)) {
                        yield return obj;
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                } finally {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
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

            foreach (var descriptor in root.GetComponentsInChildren<VRC_SceneDescriptor>(true)) {
                yield return descriptor;
            }
        }

        private static void ClearGeneratedPrefabInstanceModifications(GameObject root) {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) {
                if (transform == null) continue;
                var gameObject = transform.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(gameObject)) continue;
                CleanUnnecessaryPrefabModifications(gameObject);
            }
        }

        public static void CleanUnnecessaryPrefabModifications(GameObject prefabInstanceRoot) {
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

            if (changed) {
                PrefabUtility.SetPropertyModifications(prefabInstanceRoot, keptModifications.ToArray());
            }

            // Calling SetPropertyModifications wipes out all hideFlags for the prefab, because unity is cool
            // Recalculate them as needed
            foreach (var behaviour in prefabInstanceRoot.GetComponentsInChildren<UdonBehaviour>(true)) {
                if (behaviour != null && UdonSharpEditorUtility.IsUdonSharpBehaviour(behaviour)) {
                    behaviour.hideFlags |= HideFlags.HideInInspector;
                }
            }
        }

        private static bool ShouldRemoveGeneratedProgramModification(PropertyModification modification) {
            if (modification == null) return false;

            var isUnresolved = modification.target == null;
            var isUdonBehaviour = false;
            var isUdonSharpBacker = false;
            var isUdonSharpBehaviour = modification.target is UdonSharpBehaviour;
            var usharpForcedSyncMethod = BehaviourSyncMode.Any;
            if (modification.target is UdonBehaviour ub) {
                isUdonBehaviour = true;
                isUdonSharpBacker = IsUSharpBacker(ub);
                usharpForcedSyncMethod = GetForcedSyncMethodFromUSharp(ub);
            }

            if (isUdonSharpBacker) {
                return true;
            }

            if (modification.propertyPath == "_syncMethod") {
                return isUnresolved || usharpForcedSyncMethod != BehaviourSyncMode.Any;
            }

            if (modification.propertyPath == "_udonSharpBackingUdonBehaviour") {
                return isUnresolved;
            }

            if (modification.propertyPath == "programSource") {
                return isUnresolved;
            }

            if (modification.propertyPath == "serializedProgramAsset") {
                return isUnresolved || isUdonBehaviour;
            }

            if (modification.propertyPath == "serializationData.Prefab") {
                return isUnresolved || isUdonSharpBehaviour;
            }

            var propertyPath = modification.propertyPath;
            if (propertyPath == "DynamicMaterials" || propertyPath.StartsWith("DynamicMaterials.") ||
                propertyPath == "DynamicPrefabs" || propertyPath.StartsWith("DynamicPrefabs.")) {
                return isUnresolved || modification.target is VRC_SceneDescriptor;
            }

            return false;
        }

        private static void ClearGeneratedProgramReferences(UnityEngine.Object target) {
            if (target is UdonProgramAsset) {
                ClearObjectReferenceProperty(target, Reflection.UdonProgramAssetSerializedUdonProgramAssetField);
            }
            if (target is UdonBehaviour ub) {
                ClearObjectReferenceProperty(target, Reflection.UdonBehaviourSerializedProgramAssetField);
                if (IsUSharpBacker(ub)) {
                    ClearObjectReferenceProperty(target, Reflection.programSource);
                }
            }

            if (target is VRC_SceneDescriptor descriptor) {
                var changed = false;
                if (descriptor.DynamicMaterials != null && descriptor.DynamicMaterials.Count > 0) {
                    descriptor.DynamicMaterials.Clear();
                    changed = true;
                }
                if (descriptor.DynamicPrefabs != null && descriptor.DynamicPrefabs.Count > 0) {
                    descriptor.DynamicPrefabs.Clear();
                    changed = true;
                }
                if (changed) {
                    EditorUtility.SetDirty(descriptor);
                }
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

        public static bool IsUSharpBacker(UdonBehaviour ub) {
            return ub.GetComponents<UdonSharpBehaviour>()
                .Any(usb => UdonSharpEditorUtility.GetBackingUdonBehaviour(usb) == ub);
        }

        public static BehaviourSyncMode GetForcedSyncMethodFromUSharp(UdonBehaviour ub) {
            var program = StoreUdonSharpProgramsInTempFolderHook.programSource_get(ub);
            if (program is UdonSharpProgramAsset usp) {
                return usp.behaviourSyncMode;
            }
            return BehaviourSyncMode.Any;
        }

    }
}
