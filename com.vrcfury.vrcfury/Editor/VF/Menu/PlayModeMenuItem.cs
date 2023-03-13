using UnityEditor;

namespace VF.Menu {
    [InitializeOnLoad]
    public class PlayModeMenuItem {
        private const string EditorPref = "com.vrcfury.playMode";

        static PlayModeMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.playMode, Get());
        }

        [MenuItem(MenuItems.playMode, priority = MenuItems.playModePriority)]
        private static void Click() {
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}