using UnityEditor;

namespace VF.Menu {
    [InitializeOnLoad]
    public class AutoUploadMenuItem {
        private const string EditorPref = "com.vrcfury.autoUpload";

        static AutoUploadMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, false);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.autoUpload_name, Get());
        }

        [MenuItem(MenuItems.autoUpload_name, priority = MenuItems.autoUpload_priority)]
        private static void Click() {
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}