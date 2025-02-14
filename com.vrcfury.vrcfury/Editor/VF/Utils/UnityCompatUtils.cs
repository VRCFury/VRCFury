using System;
using System.Reflection;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class UnityCompatUtils {
        private abstract class Reflection : ReflectionHelper {
            private static readonly Type PrefabStageUtility =
#if UNITY_2022_1_OR_NEWER
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.PrefabStageUtility");
#else
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Experimental.SceneManagement.PrefabStageUtility");
#endif
            private static readonly Type PrefabStage =
#if UNITY_2022_1_OR_NEWER
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.PrefabStage");
#else
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Experimental.SceneManagement.PrefabStage");
#endif
            public delegate object GetCurrentPrefabStage_();
            public static readonly GetCurrentPrefabStage_ GetCurrentPrefabStage = PrefabStageUtility?.GetMatchingDelegate<GetCurrentPrefabStage_>("GetCurrentPrefabStage");
            public delegate object OpenPrefab_(string prefabAssetPath, GameObject openedFromInstance);
            public static readonly OpenPrefab_ OpenPrefab = PrefabStageUtility?.GetMatchingDelegate<OpenPrefab_>("OpenPrefab");
            public static readonly PropertyInfo autoSave = PrefabStage?.GetProperty("autoSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
        public static void OpenPrefab(string path, VFGameObject focus) {
            Reflection.OpenPrefab?.Invoke(path, focus);
        }

        public static bool IsEditingPrefab() {
            return Reflection.GetCurrentPrefabStage?.Invoke() != null;
        }
        
        public static bool DisablePrefabAutosave() {
            var prefabStage = Reflection.GetCurrentPrefabStage?.Invoke();
            if (prefabStage == null) return false;
            if (Reflection.autoSave == null) return false;
            var isOn = (bool)Reflection.autoSave.GetValue(prefabStage);
            if (!isOn) return false;
            Reflection.autoSave?.SetValue(prefabStage, false);
            return true;
        }
    }
}
