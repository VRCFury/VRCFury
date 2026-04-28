using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VF.Hooks.UdonCleaner;
using VF.Utils;

namespace VF.Menu {
    internal static class UdonCleanerMenuItem {
        private static readonly object initLock = new object();
        private static int loaded;
        private static bool enabled;

        private static void EnsureLoaded() {
            if (Volatile.Read(ref loaded) == 1) return;
            lock (initLock) {
                if (loaded == 1) return;
                enabled = File.Exists(GetFlagPath());
                Volatile.Write(ref loaded, 1);
            }
        }

        private static string GetFlagPath() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, "ProjectSettings", "VRCFury.SimplifyUdonSerialization.flag");
        }

        public static bool Get() {
            EnsureLoaded();
            return enabled;
        }

        [MenuItem(MenuItems.simplifyUdonSerialization, priority = MenuItems.simplifyUdonSerializationPriority)]
        private static void Click() {
            var next = !Get();
            if (next) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "!! THIS VRCFURY FEATURE IS EXPERIMENTAL !!\n\n" +
                    "TAKE A PROJECT BACKUP FIRST\n\n" +
                    "When enabled:" +
                    "* Udon and U# will save less temporary junk to disk\n" +
                    "* Udon properties will not randomly change in your scene and prefabs when you save them\n" +
                    "* Prefab overrides won't list all udon components for no reason.\n" +
                    "* U# 'None' synced behaviours can be mixed with synced behaviours on the same object\n" +
                    "* U# scripts will no longer have or need stupid .asset files alongside them\n" +
                    "\n" +
                    "This is reversible, but to do so, you MUST disable this option in VRCFury, do not just remove the plugin\n\n" +
                    "Are you sure you want to enable it?",
                    "Yes, Enable It",
                    "Cancel"
                );
                if (!ok) return;
                var flagPath = GetFlagPath();
                File.WriteAllText(flagPath, "enabled");
                lock (initLock) {
                    enabled = true;
                    Volatile.Write(ref loaded, 1);
                }
                EditorUtility.RequestScriptReload();
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
                lock (initLock) {
                    enabled = false;
                    Volatile.Write(ref loaded, 1);
                }
                StoreUdonSharpProgramsInTempFolderHook.Revert();
                UdonSharpPrefabLinksStorageHook.ClearCache();
                EditorUtility.RequestScriptReload();
            }
        }

        [MenuItem(MenuItems.simplifyUdonSerialization, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.simplifyUdonSerialization, Get());
            return true;
        }
    }
}
