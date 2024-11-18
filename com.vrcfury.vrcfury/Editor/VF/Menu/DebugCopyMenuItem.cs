using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class DebugCopyMenuItem {
        private const string Key = "com.vrcfury.createDebugCopy";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return SessionState.GetBool(Key, false);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.debugCopy, Get());
        }

        [MenuItem(MenuItems.debugCopy, priority = MenuItems.debugCopyPriority)]
        private static void Click() {
            if (!Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Enabling this option will cause VRCFury to create a backup of your avatar's controllers immediately before and after it runs during each build." +
                    " This can really slow down your build, so only enable this if needed by VRCFury support.\n\n" +
                    "Are you sure you want to continue?",
                    "Create extra copies of controllers during build",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
            UpdateMenu();
        }
    }
}