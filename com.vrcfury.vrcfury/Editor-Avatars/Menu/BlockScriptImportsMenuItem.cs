using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class BlockScriptImportsMenuItem {
        private const string Key = "com.vrcfury.blockScriptImports";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return SessionState.GetBool(Key, false);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.blockScriptImports, Get());
        }

        [MenuItem(MenuItems.blockScriptImports, priority = MenuItems.blockScriptImportsPriority)]
        private static void Click() {
            if (!Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "This will block unitypackage imports from bringing in new scripts until unity is restarted.\n\n" +
                    "Are you sure you want to continue?",
                    "Block script imports until unity restarts",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
            UpdateMenu();
        }
    }
}
