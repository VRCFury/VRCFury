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
                actions.CreateDirectory(await Markers.InstallInProgressMarker());
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
            var showUpgradeNotice = false;
            var updaterJustUpdatedMarker = await Markers.UpdaterJustUpdatedMarker();
            var legacyDir = await AsyncUtils.InMainThread(() => AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            if (!string.IsNullOrWhiteSpace(legacyDir) && Directory.Exists(legacyDir)) {
                DebugLog($"VRCFury found a legacy install at location: {legacyDir}");
                actions.SceneCloseNeeded();
                actions.RemoveDirectory(legacyDir);
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury")) {
                DebugLog($"VRCFury found a legacy install at location: Assets/VRCFury");
                actions.SceneCloseNeeded();
                actions.RemoveDirectory("Assets/VRCFury");
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury-installer")) {
                DebugLog("Installer directory found, removing and forcing update");
                actions.RemoveDirectory("Assets/VRCFury-installer");
                triggerUpgrade = true;
            }

            if (showUpgradeNotice) {
                await AsyncUtils.DisplayDialog(
                    "Please note that Assets/VRCFury/Prefabs is moving to Packages/VRCFury Prefabs"
                );
            }
            if (triggerUpgrade) {
                actions.RemoveDirectory(updaterJustUpdatedMarker);
                await VRCFuryUpdater.AddUpdateActions(false, actions);
                await actions.Run();
                return;
            }
            
            if (Directory.Exists(updaterJustUpdatedMarker)) {
                DebugLog("Updater was just reinstalled, forcing update");
                actions.RemoveDirectory(updaterJustUpdatedMarker);
                await VRCFuryUpdater.AddUpdateActions(true, actions);
                await actions.Run();
                return;
            }

            var manualUpdateInProgressMarker = await Markers.ManualUpdateInProgressMarker();
            var installInProgressMarker = await Markers.InstallInProgressMarker();
            if (Directory.Exists(manualUpdateInProgressMarker) || Directory.Exists(installInProgressMarker)) {
                DebugLog("Found 'update complete' marker");
                if (Directory.Exists(manualUpdateInProgressMarker)) Directory.Delete(manualUpdateInProgressMarker);
                if (Directory.Exists(installInProgressMarker)) Directory.Delete(installInProgressMarker);

                await AsyncUtils.InMainThread(() => {
                    DebugLog("Upgrade complete");
                });

                await AsyncUtils.DisplayDialog(
                    "VRCFury has successfully installed/updated!"
                );
            }
        }
    }
}
