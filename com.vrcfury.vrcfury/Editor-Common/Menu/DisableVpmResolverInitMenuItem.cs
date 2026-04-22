using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Menu {
    internal static class DisableVpmResolverInitMenuItem {
        private const string EditorPref = "com.vrcfury.disableVpmResolver";

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, false);
        }

        [MenuItem(MenuItems.disableVpmResolverInit, priority = MenuItems.disableVpmResolverInitPriority)]
        private static void Click() {
            var enabling = !Get();
            if (enabling) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "This will disable VRChat's buggy in-editor VPM resolver.\n\n" +
                    "Note that the editor will no longer warn you if your vcc manifest no longer matches the state of the project. You will have to re-resolve the packages in the VCC or ALCOM manually if that happens.\n\n" +
                    "Are you sure?",
                    "Yes, disable it",
                    "Cancel"
                );
                if (!ok) return;
            }

            EditorPrefs.SetBool(EditorPref, !Get());
        }

        [MenuItem(MenuItems.disableVpmResolverInit, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.disableVpmResolverInit, Get());
            return true;
        }
    }
}
