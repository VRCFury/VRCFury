using UnityEditor;

namespace VF.Menu {
    internal class BoundingBoxMenuItem {
        private const string EditorPref = "com.vrcfury.boundingBoxFix";

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.boundingBoxFix, Get());
        }

        [MenuItem(MenuItems.boundingBoxFix, priority = MenuItems.boundingBoxFixPriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
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
            UpdateMenu();
        }
    }
}
