using UnityEditor;

namespace VF.Menu {
    internal class UseInUploadMenuItem {
        private const string Key = "com.vrcfury.useInUpload";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return SessionState.GetBool(Key, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.uploadMode, Get());
        }

        [MenuItem(MenuItems.uploadMode, priority = MenuItems.uploadModePriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
                    "Warning",
                    "Disabling this option will prevent VRCFury from processing your avatar while uploading." +
                    " This means NOTHING added by VRCFury will work properly on your uploaded avatar, such as VRCFury toggles, linked clothing, merged controllers, etc." +
                    " This should only be done if you are testing to see if VRCFury is breaking something unrelated on your avatar.\n\n" +
                    "This option will automatically be re-enabled when unity restarts.\n\n" +
                    "Are you sure you want to continue?",
                    "Disable VRCFury completely until unity restarts",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
            UpdateMenu();
        }
    }
}