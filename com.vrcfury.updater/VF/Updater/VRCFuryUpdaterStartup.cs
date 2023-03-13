using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Updater {
    [InitializeOnLoad]
    public class VRCFuryUpdaterStartup { 
        static VRCFuryUpdaterStartup() {
            
            Task.Run(Check);
        }

        private static async Task<string> GetAppRootDir() {
            return Path.GetDirectoryName(await AsyncUtils.InMainThread(() => Application.dataPath));
        }

        public static async Task<string> GetUpdatedMarkerPath() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdated";
        }
        
        public static async Task<string> GetUpdateAllMarker() { 
            return await GetAppRootDir() + "/Temp/vrcfUpdateAll";
        }

        private static async void Check() {
            await AsyncUtils.ErrorDialogBoundary(() => AsyncUtils.PreventReload(CheckUnsafe));
        }

        private static async Task CheckUnsafe() {
            await AsyncUtils.InMainThread(EditorUtility.ClearProgressBar);
            
            var isRunningInsidePackage = Assembly.GetExecutingAssembly().GetName().Name == "VRCFury-Updater2";

            void DebugLog(string msg) {
                var suffix = isRunningInsidePackage ? "" : " (installer)";
                Debug.Log($"VRCFury Updater{suffix}: {msg}");
            }

            DebugLog("Checking for updates...");
            
            var packages = await AsyncUtils.ListInstalledPacakges();
            if (!packages.Any(p => p.name == "com.vrcfury.updater")) {
                // Updater package (... this package) isn't installed, which means this code
                // is probably running inside of the standalone installer, and we need to go install
                // the updater and main vrcfury package.
                DebugLog("Package is missing, bootstrapping com.vrcfury.updater package");
                await VRCFuryUpdater.UpdateAll();
                return;
            }

            if (!isRunningInsidePackage) {
                return;
            }
            
            await SceneCloser.ReopenScenes();

            var triggerUpgrade = false;
            var showUpgradeNotice = false;
            var updateAllMarker = await GetUpdateAllMarker();
            var legacyDir = await AsyncUtils.InMainThread(() => AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            if (!string.IsNullOrWhiteSpace(legacyDir) && Directory.Exists(legacyDir)) {
                DebugLog($"VRCFury found a legacy install at location: {legacyDir}");
                await SceneCloser.CloseScenes();
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset(legacyDir));
                await SceneCloser.ReopenScenes();
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury")) {
                DebugLog($"VRCFury found a legacy install at location: Assets/VRCFury");
                await SceneCloser.CloseScenes();
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset("Assets/VRCFury"));
                await SceneCloser.ReopenScenes();
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury-installer")) {
                DebugLog("Installer directory found, removing and forcing update");
                await SceneCloser.CloseScenes();
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset("Assets/VRCFury-installer"));
                await SceneCloser.ReopenScenes();
                triggerUpgrade = true;
            }

            if (showUpgradeNotice) {
                await AsyncUtils.DisplayDialog(
                    "VRCFury is upgrading to a unity package, so it's moving from Assets/VRCFury to Packages/VRCFury. Don't worry, nothing else should change!"
                );
            }
            if (triggerUpgrade) {
                if (Directory.Exists(updateAllMarker)) Directory.Delete(updateAllMarker);
                await VRCFuryUpdater.UpdateAll();
                return;
            }
            
            if (Directory.Exists(updateAllMarker)) {
                DebugLog("Updater was just reinstalled, forcing update");
                Directory.Delete(updateAllMarker);
                await VRCFuryUpdater.UpdateAll(true);
                return;
            }

            var updatedMarker = await GetUpdatedMarkerPath();
            if (Directory.Exists(updatedMarker)) {
                DebugLog("Found 'update complete' marker");
                Directory.Delete(updatedMarker);

                await AsyncUtils.InMainThread(() => {
                    EditorUtility.ClearProgressBar();
                    DebugLog("Upgrade complete");
                });

                await AsyncUtils.DisplayDialog(
                    "VRCFury has successfully installed/updated!"
                );
            }
        }
    }
}
