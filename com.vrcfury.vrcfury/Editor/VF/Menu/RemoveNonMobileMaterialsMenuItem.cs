using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class RemoveNonMobileMaterialsMenuItem {
        private const string EditorPref = "com.vrcfury.removeNonMobileMaterials";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.removeNonMobileMaterials, Get());
        }

        [MenuItem(MenuItems.removeNonMobileMaterials, priority = MenuItems.removeNonMobileMaterialsPriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this option can result in your avatar failing to upload!" +
                    "Only disable this if a later build step (eg. VRCQT) is going to convert non-mobile compatible materials.\n" +
                    "Are you sure you want to continue?",
                    "Yes, disable non-mobile material removal",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}
