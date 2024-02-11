using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Builder;

namespace VF.Menu {
    [InitializeOnLoad]
    public class PlayModeMenuItem {
        private const string EditorPref = "com.vrcfury.playMode";

        static PlayModeMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.playMode, Get());
        }

        [MenuItem(MenuItems.playMode, priority = MenuItems.playModePriority)]
        private static void Click() {
            if (Get()) {
                var ok = EditorUtility.DisplayDialog(
                    "Warning",
                    "Disabling this option will cause VRCFury-added features to not function AT ALL while in play mode. Are you sure you want to continue?",
                    "Yes, do not run VRCFury in play mode",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
    }
}