using UnityEditor;

namespace VF.Menu {
    public static class AutoUpgradeDpsMenuItem {
        private const string EditorPref = "com.vrcfury.autoUpgradeDps";

        [InitializeOnLoadMethod]
        public static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.dpsAutoUpgrade, Get());
        }

        [MenuItem(MenuItems.dpsAutoUpgrade, priority = MenuItems.dpsAutoUpgradePriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
                    "Warning",
                    "Disabling this option will prevent meshes with DPS from being able to trigger haptics and" +
                    " animations on other avatars. Are you sure you want to continue?",
                    "Yes, do not add contacts to DPS",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}
