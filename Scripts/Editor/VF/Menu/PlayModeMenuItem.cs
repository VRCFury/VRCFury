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
            UnityEditor.Menu.SetChecked(MenuItems.playMode_name, Get());
        }

        [MenuItem(MenuItems.playMode_name, priority = MenuItems.playMode_priority)]
        private static void Click() {
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}