using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class AutoUpgradeConstraintsMenuItem {
        private const string EditorPref = "com.vrcfury.autoUpgradeConstraints";

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }

        [MenuItem(MenuItems.autoUpgradeConstraints, priority = MenuItems.autoUpgradeConstraintsPriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this option can reduce in-game performance and can break assets which have been" +
                    " 'Half-Upgraded' (meaning only the controllers or only the objects have been upgraded to VRC Constraints)." +
                    " Are you sure you want to continue?",
                    "Yes, disable constraint conversion",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.autoUpgradeConstraints, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.autoUpgradeConstraints, Get());
            return true;
        }
    }
}
