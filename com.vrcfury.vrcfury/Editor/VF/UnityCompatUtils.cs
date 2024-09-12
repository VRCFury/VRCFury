using VF.Builder;

namespace VF {
    internal static class UnityCompatUtils {
        public static void OpenPrefab(string path, VFGameObject focus) {
            UnityReflection.PrefabStage.OpenPrefab?.Invoke(path, focus);
        }

        public static bool IsEditingPrefab() {
            return UnityReflection.PrefabStage.GetCurrentPrefabStage?.Invoke() != null;
        }
        
        public static bool DisablePrefabAutosave() {
            var prefabStage = UnityReflection.PrefabStage.GetCurrentPrefabStage?.Invoke();
            if (prefabStage == null) return false;
            if (UnityReflection.PrefabStage.autoSave == null) return false;
            var isOn = (bool)UnityReflection.PrefabStage.autoSave.GetValue(prefabStage);
            if (!isOn) return false;
            UnityReflection.PrefabStage.autoSave?.SetValue(prefabStage, false);
            return true;
        }
    }
}
