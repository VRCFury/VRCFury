using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

namespace VF {
    
    /**
     * Unity 2019 has a bug where if you have prefab auto-save turned on, and open a prefab in an immutable package,
     * it will prevent you from leaving the prefab editor forever, and you can't even change the auto-save checkbox.
     * This class will ensure auto-save is turned off any time you edit a prefab in a vrcfury prefab.
     */
    [InitializeOnLoad]
    public class ImmutablePrefabFixer {
        public static Action DisableAutosave;
        
        static ImmutablePrefabFixer() {
            var SceneNavigationManager =
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.StageNavigationManager");
            if (SceneNavigationManager == null) {
                Debug.LogError("Failed to find SceneNavigationManager");
                return;
            }

            var instanceField = SceneNavigationManager.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (instanceField == null) {
                Debug.LogError("Failed to find instance field");
                return;
            }

            var instance = instanceField.GetValue(null);
            if (instance == null) {
                Debug.LogError("Failed to find instance");
                return;
            }

            var autoSaveField = SceneNavigationManager.GetProperty("autoSave",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (autoSaveField == null) {
                Debug.LogError("Failed to find autoSave field");
                return;
            }

            DisableAutosave = () => autoSaveField.SetValue(instance, false);

            EditorApplication.update += Update;
        }

        private static void Update() {
            try {
                if (IsEditingVrcfuryPrefab()) {
                    DisableAutosave();
                }
            } catch (Exception e) {
                Debug.LogError(e);
            }
        }

        private static bool IsEditingVrcfuryPrefab() {
            var path = GetEditingPrefabPath();
            return path != null && path.Contains("vrcfury");
        }

        public static string GetEditingPrefabPath() {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return null;
            return stage.prefabAssetPath;
        }
    }
}
