using System.Collections.Immutable;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;
using VRC.Udon;

namespace VF.Hooks.UdonCleaner {
    internal static class UdonCleanerUninstaller {

        public static void Uninstall() {

            Debug.Log("Reorganizing program assets ...");
            UdonCleanerAssetManager.Reorganize(UdonCleanerAssetManager.Layout.VANILLA);
            UdonCleanerReflection.ClearProgramAssetCache();
            AssetDatabase.SaveAssets();

            Debug.Log("Repairing prefabs ...");
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                foreach (var path in AssetDatabase.FindAssets("t:Prefab")
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Where(path => !string.IsNullOrEmpty(path))) {
                    RepairPrefab(path);
                }
            });

            Debug.Log("Repairing open scenes ...");
            var changedOpenScenes = false;
            foreach (var root in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt)
                         .Where(scene => scene.isLoaded)
                         .SelectMany(scene => scene.Roots())
                     ) {
                changedOpenScenes |= RepairRoot(root);
            }
            if (changedOpenScenes) {
                Debug.Log("Saving open scenes ...");
                EditorSceneManager.SaveOpenScenes();
            }

            var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded)
                .Select(scene => scene.path)
                .ToImmutableHashSet();
            var unloadedScenes = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => !loadedScenes.Contains(path))
                .ToArray();
            if (unloadedScenes.Any()) {
                Debug.Log("Repairing unloaded scenes ...");
                foreach (var path in unloadedScenes) {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try {
                        var changed = false;
                        foreach (var root in scene.Roots()) {
                            changed |= RepairRoot(root);
                        }
                        if (changed) {
                            //Debug.Log($"Saving {path} ...");
                            EditorSceneManager.SaveScene(scene);
                        }
                    } finally {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            Debug.Log("Done");
        }

        private static void RepairPrefab(string path) {
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try {
                var changed = RepairRoot(prefabRoot);
                if (changed) {
                    //Debug.Log($"Saving {path} ...");
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
            } finally {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        private static bool RepairRoot(VFGameObject root) {
            var changed = false;
            foreach (var ub in root.GetComponentsInSelfAndChildren<UdonBehaviour>()) {
                changed |= RepairBehaviour(ub);
            }
            return changed;
        }
        private static bool RepairBehaviour(UdonBehaviour ub) {
            var usb = ub.GetComponents<UdonSharpBehaviour>()
                .FirstOrDefault(x => UdonSharpEditorUtility.GetBackingUdonBehaviour(x) == ub);
            if (usb == null) return false;

            var so = new SerializedObject(ub);
            so.UpdateIfRequiredOrScript();

            var programSource = so.FindProperty("programSource");
            var serializedProgramAsset = so.FindProperty("serializedProgramAsset");

            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(ub);
            if (prefabSource != null) {
                var changed = false;
                if (programSource.prefabOverride) {
                    PrefabUtility.RevertPropertyOverride(programSource, InteractionMode.AutomatedAction);
                    changed = true;
                }
                if (serializedProgramAsset.prefabOverride) {
                    PrefabUtility.RevertPropertyOverride(serializedProgramAsset, InteractionMode.AutomatedAction);
                    changed = true;
                }
                return changed;
            }

            var script = MonoScript.FromMonoBehaviour(usb);
            programSource.objectReferenceValue = null;
            serializedProgramAsset.objectReferenceValue = null;
            if (UdonCleanerAssetManager._udonSharpMonoScriptToProgram.TryGetValue(script, out var program)) {
                programSource.objectReferenceValue = program;
                if (UdonCleanerAssetManager._serializedCache.TryGetValue(program, out var serialized)) {
                    serializedProgramAsset.objectReferenceValue = serialized;
                }
            }
            return so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
