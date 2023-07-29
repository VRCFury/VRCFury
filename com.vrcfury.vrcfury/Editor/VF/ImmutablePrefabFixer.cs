using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF {
    
    /**
     * Unity 2019 has a bug where if you have prefab auto-save turned on, and open a prefab in an immutable package,
     * it will prevent you from leaving the prefab editor forever, and you can't even change the auto-save checkbox.
     * This class will ensure auto-save is turned off any time you edit a prefab in a vrcfury prefab.
     */
    [InitializeOnLoad]
    public class ImmutablePrefabFixer {
        private static Func<string> GetPrefabStagePath;
        private static Action DisableAutosave;
        
        static ImmutablePrefabFixer() {
            var stageUtility = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.PrefabStageUtility");
            if (stageUtility == null) {
                stageUtility = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Experimental.SceneManagement.PrefabStageUtility");
            }
            if (stageUtility == null) {
                throw new Exception("Failed to find PrefabStageUtility");
            }
            var getCurrentStage = stageUtility.GetMethod("GetCurrentPrefabStage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCurrentStage == null) {
                throw new Exception("Failed to find GetCurrentPrefabStage");
            }
            GetPrefabStagePath = () => {
                var stage = getCurrentStage.Invoke(null, new object[] { });
                if (stage == null) return null;
                var pathProp = stage.GetType().GetProperty("prefabAssetPath");
                if (pathProp == null) {
                    throw new Exception("Failed to find prefabAssetPath");
                }
                return pathProp.GetValue(stage) as string;
            };

            var SceneNavigationManager =
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.StageNavigationManager");
            if (SceneNavigationManager == null) {
                throw new Exception("Failed to find SceneNavigationManager");
            }

            var instanceField = SceneNavigationManager.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (instanceField == null) {
                throw new Exception("Failed to find instance field");
            }

            var instance = instanceField.GetValue(null);
            if (instance == null) {
                throw new Exception("Failed to find instance");
            }

            var autoSaveField = SceneNavigationManager.GetProperty("autoSave",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (autoSaveField == null) {
                throw new Exception("Failed to find autoSave field");
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
            return GetPrefabStagePath();
        }
    }
}
