using UnityEditor;

namespace VF.Menu {
    [InitializeOnLoad]
    public class HapticsToggleMenuItem {
        private const string EditorPref = "com.vrcfury.haptics";

        static HapticsToggleMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.hapticToggle, Get());
        }

        [MenuItem(MenuItems.hapticToggle, priority = MenuItems.hapticTogglePriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
                    "Warning",
                    "Disabling haptic contacts will completely break integration with haptic response applications," +
                    " and is typically only needed if your avatar is completely out of available contacts. Are you sure you want to continue?",
                    "Yes, disable all haptic support",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}