using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Hooks.UdonCleaner;
using VF.Utils;

namespace VF.Menu {
    internal static class SimplifyUdonSerializationMenuItem {
        private static bool enabled;

        [InitializeOnLoadMethod]
        private static void Init() {
            enabled = File.Exists(GetFlagPath());
        }

        private static string GetFlagPath() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, "ProjectSettings", "VRCFury.SimplifyUdonSerialization.flag");
        }

        public static bool Get() {
            return enabled;
        }

        [MenuItem(MenuItems.simplifyUdonSerialization, priority = MenuItems.simplifyUdonSerializationPriority)]
        private static void Click() {
            var next = !Get();
            if (next) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "!! THIS VRCFURY FEATURE IS EXPERIMENTAL PRE-ALPHA !!\n\n" +
                    "When enabled, VRCFury will patch the Udon/U# Editor to be more backup/diff/scm/override compatible. " +
                    "Fewer udon properties will randomly change when you save your project, and prefab overrides won't list " +
                    "all udon components for no reason.\n\n" +
                    "Note: SerialiedUdonAssets should be added to your .gitignore if you use this feature.\n\n" +
                    "Are you sure you want to enable it?",
                    "Yes, Enable It",
                    "Cancel"
                );
                if (!ok) return;
                var flagPath = GetFlagPath();
                File.WriteAllText(flagPath, "enabled");
                enabled = true;
            } else {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Disabling this will cause udon junk to start filling your asset files again.\n\n" +
                    "Continue?",
                    "Yes, Disable it",
                    "Cancel"
                );
                if (!ok) return;
                var flagPath = GetFlagPath();
                if (File.Exists(flagPath)) {
                    File.Delete(flagPath);
                }
                enabled = false;
                UdonSharpPrefabLinksStorageHook.ClearCache();
            }
        }

        [MenuItem(MenuItems.simplifyUdonSerialization, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.simplifyUdonSerialization, Get());
            return true;
        }
    }
}
