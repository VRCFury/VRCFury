using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class DisableDbtOptimizerMenuItem {
        private const string Key = "com.vrcfury.disableDbt";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return SessionState.GetBool(Key, false);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.disableDbtMerging, Get());
        }

        [MenuItem(MenuItems.disableDbtMerging, priority = MenuItems.disableDbtMergingPriority)]
        private static void Click() {
            if (!Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Enabling this option will prevent VRCFury from merging layers into an optimized DBT." +
                    " This is mostly for debugging purposes (it's easier to tell what each layer is doing)." +
                    " Beware that this will be VERY BAD FOR PERFORMANCE!\n\nThis option will reset when unity restarts.\n\n" +
                    "Are you sure you want to continue?",
                    "Yes, disable DBT merging, my avatar will be awful performance",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
            UpdateMenu();
        }
    }
}
