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
                    " Just beware that in play mode, your avatar with have a TON of layers!\n\n" +
                    "Are you sure you want to continue?",
                    "Yes, disable DBT merging",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
            UpdateMenu();
        }
    }
}
