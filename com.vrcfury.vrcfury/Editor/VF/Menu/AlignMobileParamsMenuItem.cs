using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class AlignMobileParamsMenuItem {
        private const string EditorPref = "com.vrcfury.alignMobile";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.alignMobile, Get());
        }

        [MenuItem(MenuItems.alignMobile, priority = MenuItems.alignMobilePriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this option makes properly synchronizing avatars between desktop and mobile very difficult." +
                    " When disabled, you must ensure manually that the parameter files match EXACTLY on both desktop and mobile," +
                    " and this may not even be possible if non-destructive tools like VRCFury are used on the avatar." +
                    " Are you sure you want to continue?",
                    "Yes, disable mobile parameter alignment",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}
