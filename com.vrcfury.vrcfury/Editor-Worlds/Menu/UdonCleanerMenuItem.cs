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

        public class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
                if (!didDomainReload) return;
                if (!File.Exists(GetUninstallFlagPath())) return;
                File.Delete(GetUninstallFlagPath());
                UdonCleanerUninstall.Uninstall();
                EditorUtility.RequestScriptReload();
            }
        }

        private static string GetFlagPath() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, "ProjectSettings", "VRCFury.SimplifyUdonSerialization.flag");
        }

        private static string GetUninstallFlagPath() {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, "ProjectSettings", "VRCFury.UninstallUdonCleaner.flag");
        }

        public static bool Get() {
            EnsureLoaded();
            return enabled;
        }

        [MenuItem(MenuItems.udonCleaner, priority = MenuItems.udonCleanerPriority)]
        private static void Click() {
            var next = !Get();
            if (next) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "WARNING:\n" +
                    "* THIS VRCFURY FEATURE IS EXPERIMENTAL\n" +
                    "* TAKE A PROJECT BACKUP FIRST\n" +
                    "* Packages exported with the Udon Cleaner enabled will only be usable in projects using the Udon Cleaner." +
                    " If you are going to distribute packages, consider using a dedicated project with the cleaner disabled!\n" +
                    "* To undo this feature, you must disable this menu item, don't just uninstall VRCFury!\n" +
                    "\n" +
                    "Features:\n" +
                    "* Udon properties will not randomly change in your scene and prefabs\n" +
                    "* Prefab overrides won't list all udon components for no reason\n" +
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
                    "To uninstall Udon Cleaner, VRCFury must repopulate all the vanilla udon junk in every scene and prefab." +
                    " This can take a couple minutes.\n\n" +
                    "Continue?",
                    "Yes, Remove Udon Cleaner",
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
                File.WriteAllText(GetUninstallFlagPath(), "flag");
                EditorUtility.RequestScriptReload();
            }
        }

        [MenuItem(MenuItems.udonCleanerUninstallAgain, priority = MenuItems.udonCleanerUninstallAgainPriority)]
        private static void ClickUninstallAgain() {
            if (Get()) {
                DialogUtils.DisplayDialog(
                    "Warning",
                    "You can't run this if Udon Cleaner is already enabled",
                    "Ok"
                );
                return;
            }
            var ok = DialogUtils.DisplayDialog(
                "Warning",
                "Only run this if you were asked on the vrcfury discord. " +
                "Running this at the wrong time can blow up your project.\n\n" +
                "Continue?",
                "Yes, I'm sure, things may break forever",
                "Cancel"
            );
            if (!ok) return;
            UdonCleanerUninstall.Uninstall();
            EditorUtility.RequestScriptReload();
        }

        [MenuItem(MenuItems.udonCleaner, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.udonCleaner, Get());
            return true;
        }
    }
}
