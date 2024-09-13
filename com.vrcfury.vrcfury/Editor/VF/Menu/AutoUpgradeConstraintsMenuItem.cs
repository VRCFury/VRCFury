using UnityEditor;

namespace VF.Menu {
    internal static class AutoUpgradeConstraintsMenuItem {
        private const string EditorPref = "com.vrcfury.autoUpgradeConstraints";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.autoUpgradeConstraints, Get());
        }

        [MenuItem(MenuItems.autoUpgradeConstraints, priority = MenuItems.autoUpgradeConstraintsPriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
                    "Warning",
                    "Disabling this option can reduce in-game performance and can break assets which have been" +
                    " 'Half-Upgraded' (meaning only the controllers or only the objects have been upgraded to VRC Constraints)." +
                    " Are you sure you want to continue?",
                    "Yes, disable constraint conversion",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}
