using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class AutoUpgradeDpsMenuItem {
        private const string EditorPref = "com.vrcfury.autoUpgradeDps";

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }

        [MenuItem(MenuItems.dpsAutoUpgrade, priority = MenuItems.dpsAutoUpgradePriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this option will prevent your DPS/TPS assets from triggering haptics or" +
                    " triggering depth animations, and may prevent them from working entirely in certain worlds" +
                    " or future versions of VRChat.\n\n" +
                    " Are you sure you want to continue?",
                    "Yes, never upgrade DPS/TPS",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.dpsAutoUpgrade, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.dpsAutoUpgrade, Get());
            return true;
        }
    }
}
