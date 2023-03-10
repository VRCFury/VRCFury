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

            var triggerUpgrade = false;
            var showUpgradeNotice = false;
            var legacyDir = await AsyncUtils.InMainThread(() => AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            if (!string.IsNullOrWhiteSpace(legacyDir) && Directory.Exists(legacyDir)) {
                DebugLog($"VRCFury found a legacy install at location: {legacyDir}");
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset(legacyDir));
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury")) {
                DebugLog($"VRCFury found a legacy install at location: Assets/VRCFury");
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset("Assets/VRCFury"));
                triggerUpgrade = true;
                showUpgradeNotice = true;
            }
            if (Directory.Exists("Assets/VRCFury-installer")) {
                DebugLog("Installer directory found, removing and forcing update");
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset("Assets/VRCFury-installer"));
                triggerUpgrade = true;
            }

            if (showUpgradeNotice) {
                await AsyncUtils.DisplayDialog(
                    "VRCFury is upgrading to a unity package, so it's moving from Assets/VRCFury to Packages/VRCFury. Don't worry, nothing else should change!"
                );
            }
            if (triggerUpgrade) {
                await VRCFuryUpdater.UpdateAll();
                return;
            }

            var updateAllMarker = await GetUpdateAllMarker();
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

                // We need to reload scenes. If we do not, any serialized data with a changed type will be deserialized as "null"
                // This is especially common for fields that we change from a non-guid type to a guid type, like
                // AnimationClip to GuidAnimationClip.
                await AsyncUtils.InMainThread(() => {
                    var openScenes = Enumerable.Range(0, SceneManager.sceneCount)
                        .Select(i => SceneManager.GetSceneAt(i))
                        .Where(scene => scene.isLoaded);
                    foreach (var scene in openScenes) {
                        var type = typeof(EditorSceneManager);
                        var method = type.GetMethod("ReloadScene", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                        method.Invoke(null, new object[] { scene });
                    }

                    EditorUtility.ClearProgressBar();
                    DebugLog("Upgrade complete");
                });
                
                await AsyncUtils.DisplayDialog(
                    "VRCFury has been updated.\n\nUnity may be frozen for a bit as it reloads."
                );
            }
        }
    }
}
