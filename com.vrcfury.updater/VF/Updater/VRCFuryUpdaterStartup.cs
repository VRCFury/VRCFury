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
            var packages = await AsyncUtils.ListInstalledPacakges();
            if (!packages.Any(p => p.name == "com.vrcfury.updater")) {
                // Updater package (... this package) isn't installed, which means this code
                // is probably running inside of the standalone installer, and we need to go install
                // the updater and main vrcfury package.
                Debug.Log("VRCFury Updater: Installer detected, bootstrapping com.vrcfury.updater package");
                await VRCFuryUpdater.UpdateAll();
                return;
            }

            if (Directory.Exists("Assets/VRCFury-installer")) {
                if (Assembly.GetExecutingAssembly().FullName == "VRCFury-Updater2") {
                    // There are two of us! The Assets copy is in charge for upgrading "us" (the package)
                    return;
                }
                Debug.Log("VRCFury Updater: Installer directory found, removing and forcing update");
                await AsyncUtils.InMainThread(() => AssetDatabase.DeleteAsset("Assets/VRCFury-installer"));
                await VRCFuryUpdater.UpdateAll();
                return;
            }

            var updateAllMarker = await GetUpdateAllMarker();
            if (Directory.Exists(updateAllMarker)) {
                Debug.Log("VRCFury Updater: Updater was just reinstalled, forcing update");
                Directory.Delete(updateAllMarker);
                await VRCFuryUpdater.UpdateAll(true);
                return;
            }

            var updatedMarker = await GetUpdatedMarkerPath();
            if (Directory.Exists(updatedMarker)) {
                Debug.Log("VRCFury Updater: Found 'update complete' marker");
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
                    Debug.Log("Upgrade complete");
                    EditorUtility.DisplayDialog(
                        "VRCFury Updater",
                        "VRCFury has been updated.\n\nUnity may be frozen for a bit as it reloads.",
                        "Ok"
                    );
                });
            }
        }
    }
}
