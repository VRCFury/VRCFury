using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class UnpackWarningMenuItem {
        private const string EditorPref = "com.vrcfury.unpackWarning";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.unpackWarning, Get());
        }

        [MenuItem(MenuItems.unpackWarning, priority = MenuItems.unpackWarningPriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Unpacking prefabs can genuinely cause issues with the future maintenance and upgradability of your project.\n\n" +
                    "If you are unpacking prefabs on a regular basis, please, please consider re-evaluating your process and research if prefab variants" +
                    " or non-destructive linking systems like Armature Link can solve your problem without unpacking.",
                    "I understand the consequences, stop annoying me",
                    "Keep unpack warnings"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}