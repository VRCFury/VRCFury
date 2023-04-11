using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VF.Updater {
    [InitializeOnLoad]
    public class VRCFuryUpdaterStartup { 
        static VRCFuryUpdaterStartup() {
            if (Application.isPlaying) return;
            Task.Run(Check);
        }

        private static readonly bool IsRunningInsidePackage =
            Assembly.GetExecutingAssembly().GetName().Name == "VRCFury-Updater2";

        private static readonly bool UpdaterAssemblyExists =
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "VRCFury-Updater2");

        private static void DebugLog(string msg) {
            var suffix = IsRunningInsidePackage ? "" : " (installer)";
            Debug.Log($"VRCFury Updater{suffix}: {msg}");
        }

        private static async void Check() {
            DebugLog("Startup check started");
            await AsyncUtils.ErrorDialogBoundary(CheckUnsafe);
            DebugLog("Startup check ended");
        }

        private static async Task CheckUnsafe() {
            
            var updaterJustUpdated = await Markers.UpdaterJustUpdated();
            var upgradeFromLegacyInProgress = await Markers.UpgradeFromLegacyInProgress();
            var freshInstallInProgress = await Markers.FreshInstallInProgress();
            var manualUpdateInProgress = await Markers.ManualUpdateInProgress();

            var actions = new PackageActions(DebugLog);

            var packages = await actions.ListInstalledPacakges();
            if (!packages.Any(p => p.name == "com.vrcfury.updater")) {
                // Updater package (... this package) isn't installed, which means this code
                // is probably running inside of the standalone installer, and we need to go install
                // the updater and main vrcfury package.
                await AsyncUtils.DisplayDialog(
                    "The VRCFury Unity Package is importing, so unity may freeze or go unresponsive for a bit." +
                    " If all goes well, you'll receive a popup when it's complete!"
                );
                actions.CreateMarker(freshInstallInProgress);
                DebugLog("Package is missing, bootstrapping com.vrcfury.updater package");
                await VRCFuryUpdater.AddUpdateActions(false, actions);
                await actions.Run();
                return;
            }

            if (!IsRunningInsidePackage) {
                DebugLog("(not running inside package)");
                return;
            }
            
            await SceneCloser.ReopenScenes();

            DebugLog("Checking for migration folders ...");
            var triggerUpgrade = false;
            var legacyDir = await AsyncUtils.InMainThread(() => AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            if (!string.IsNullOrWhiteSpace(legacyDir) && Directory.Exists(legacyDir)) {
                DebugLog($"VRCFury found a legacy install at location: {legacyDir}");
                actions.SceneCloseNeeded();
                actions.RemoveDirectory(legacyDir);
                actions.CreateMarker(upgradeFromLegacyInProgress);
                triggerUpgrade = true;
            }
            if (Directory.Exists("Assets/VRCFury")) {
                DebugLog($"VRCFury found a legacy install at location: Assets/VRCFury");
                actions.SceneCloseNeeded();
                actions.RemoveDirectory("Assets/VRCFury");
                actions.CreateMarker(upgradeFromLegacyInProgress);
                triggerUpgrade = true;
            }
            if (Directory.Exists("Assets/VRCFury-installer")) {
                DebugLog("Installer directory found, removing and forcing update");
                actions.RemoveDirectory("Assets/VRCFury-installer");
                actions.CreateMarker(freshInstallInProgress);
                triggerUpgrade = true;
            }

            if (triggerUpgrade) {
                actions.RemoveMarker(updaterJustUpdated);
                await VRCFuryUpdater.AddUpdateActions(false, actions);
                await actions.Run();
                return;
            }
            
            if (updaterJustUpdated.Exists()) {
                DebugLog("Updater was just reinstalled, forcing update");
                actions.RemoveMarker(updaterJustUpdated);
                await VRCFuryUpdater.AddUpdateActions(true, actions);
                await actions.Run();
                return;
            }

            if (manualUpdateInProgress.Exists() || freshInstallInProgress.Exists() || upgradeFromLegacyInProgress.Exists()) {
                DebugLog("Found 'update complete' marker");

                if (upgradeFromLegacyInProgress.Exists()) {
                    await AsyncUtils.DisplayDialog(
                        "VRCFury has updated. Please note that Assets/VRCFury/Prefabs has moved to Packages/VRCFury Prefabs."
                    );
                } else if (freshInstallInProgress.Exists()) {
                    await AsyncUtils.DisplayDialog(
                        "VRCFury has successfully been installed!"
                    );
                } else {
                    await AsyncUtils.DisplayDialog(
                        "VRCFury has successfully updated!"
                    );
                }

                manualUpdateInProgress.Clear();
                freshInstallInProgress.Clear();
                upgradeFromLegacyInProgress.Clear();
            }
        }
    }
}
