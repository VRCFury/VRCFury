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
            EditorApplication.delayCall += () => Task.Run(Check);
        }

        private static async Task<string> GetAppRootDir() {
            return Path.GetDirectoryName(await AsyncUtils.InMainThread(() => Application.dataPath));
        }

        public static async Task<string> GetJustUpdatedMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdated";
        }
        
        public static async Task<string> GetUpdaterJustUpdatedMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdateAll";
        }
        
        private static readonly bool IsRunningInsidePackage =
            Assembly.GetExecutingAssembly().GetName().Name == "VRCFury-Updater2";

        private static readonly bool UpdatePackageExists =
            AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetName().Name == "VRCFury-Updater2");
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

            var actions = new PackageActions();

            // Don't call ListInstalledPackages here, since if we do it at the same time as another assembly,
            // it will fail to complete one of the calls

            if (!UpdatePackageExists) {
                // Updater package (... this package) isn't installed, which means this code
                // is probably running inside of the standalone installer, and we need to go install
                // the updater and main vrcfury package.
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
            var updaterJustRanMarker = await GetUpdaterJustUpdatedMarker();
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
                    "VRCFury is upgrading to a unity package, so it's moving from Assets/VRCFury to Packages/VRCFury. Don't worry, nothing else should change!"
                );
            }
            if (triggerUpgrade) {
                actions.RemoveDirectory(updaterJustRanMarker);
                await VRCFuryUpdater.AddUpdateActions(false, actions);
                await actions.Run();
                return;
            }
            
            if (Directory.Exists(updaterJustRanMarker)) {
                DebugLog("Updater was just reinstalled, forcing update");
                actions.RemoveDirectory(updaterJustRanMarker);
                await VRCFuryUpdater.AddUpdateActions(true, actions);
                await actions.Run();
                return;
            }

            var justUpdatedMarker = await GetJustUpdatedMarker();
            if (Directory.Exists(justUpdatedMarker)) {
                DebugLog("Found 'update complete' marker");
                Directory.Delete(justUpdatedMarker);

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
