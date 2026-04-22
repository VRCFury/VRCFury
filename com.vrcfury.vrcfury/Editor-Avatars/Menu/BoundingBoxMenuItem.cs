using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class BoundingBoxMenuItem {
        private const string EditorPref = "com.vrcfury.boundingBoxFix";

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }

        [MenuItem(MenuItems.boundingBoxFix, priority = MenuItems.boundingBoxFixPriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this option will prevent VRCFury from adjusting small bounding boxes." +
                    " This may cause small props on your avatar (like hair) to unexpectedly disappear for users at certain angles." +
                    " Are you sure you want to continue?",
                    "Yes, small props may break at certain angles",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.boundingBoxFix, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.boundingBoxFix, Get());
            return true;
        }
    }
}
