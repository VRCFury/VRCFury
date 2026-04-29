using System;
using System.Reflection;
using UnityEngine;

#if UNITY_2022_1_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

namespace VF.Utils {
    internal static class UnityCompatUtils {
        private abstract class Reflection : ReflectionHelper {
            public delegate object OpenPrefab_(string prefabAssetPath, GameObject openedFromInstance);
            public static readonly OpenPrefab_ OpenPrefab = typeof(PrefabStageUtility).GetMatchingDelegate<OpenPrefab_>("OpenPrefab");
            public static readonly PropertyInfo autoSave = typeof(PrefabStage).VFProperty("autoSave");
        }
        
        public static void OpenPrefab(string path, VFGameObject focus) {
            Reflection.OpenPrefab?.Invoke(path, focus);
        }

        public static bool IsEditingPrefab() {
            return PrefabStageUtility.GetCurrentPrefabStage() != null;
        }

        public static VFGameObject GetStageRoot() {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return null;
            return stage.prefabContentsRoot;
        }

        public static string GetStagePath() {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return null;
#if UNITY_2022_1_OR_NEWER
            return stage.assetPath;
#else
            return stage.prefabAssetPath;
#endif
        }
        
        public static bool DisablePrefabAutosave() {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null) return false;
            if (Reflection.autoSave == null) return false;
            var isOn = (bool)Reflection.autoSave.GetValue(prefabStage);
            if (!isOn) return false;
            Reflection.autoSave?.SetValue(prefabStage, false);
            return true;
        }

    }
}
