using UnityEditor;
using UnityEngine;
using VF.Hooks.VrcsdkFixes;
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
                    "The editor will no longer warn you if the project's VPM manifest is out of sync. " +
                    $"If that happens, re-resolve the packages manually in {AlcomUtils.GetName()}.\n\n" +
                    "Are you sure?",
                    "Yes, disable it",
                    "Cancel"
                );
                if (!ok) return;
            }

            EditorPrefs.SetBool(EditorPref, !Get());
            DisableVpmResolverAutoInitHook.Apply();
        }

        [MenuItem(MenuItems.disableVpmResolverInit, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.disableVpmResolverInit, Get());
            return true;
        }
    }
}
