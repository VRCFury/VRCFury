using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class UseInUploadMenuItem {
        private const string Key = "com.vrcfury.useInUpload";

        public static bool Get() {
            return SessionState.GetBool(Key, true);
        }

        [MenuItem(MenuItems.uploadMode, priority = MenuItems.uploadModePriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
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
        }

        [MenuItem(MenuItems.uploadMode, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.uploadMode, Get());
            return true;
        }
    }
}
