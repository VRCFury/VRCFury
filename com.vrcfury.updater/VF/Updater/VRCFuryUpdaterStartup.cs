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

        public static string GetAppRootDir() {
            return Path.GetDirectoryName(Application.dataPath);
        }

        public static string GetUpdatedMarkerPath() { 
            return GetAppRootDir() + "/Temp/vrcfUpdated";
        }
        
        public static string GetUpdateAllMarker() { 
            return GetAppRootDir() + "/Temp/vrcfUpdateAll";
        }

        private static async void Check() {
            var packages = await AsyncUtils.ListInstalledPacakges();
            if (!packages.Any(p => p.name == "com.vrcfury.updater")) {
                // Updater package (... this package) isn't installed, which means this code
                // is probably running inside of the standalone installer, and we need to go install
                // the updater and main vrcfury package.
                await VRCFuryUpdater.UpdateAll();
                return;
            }
            
            AssetDatabase.DeleteAsset("Assets/VRCFury-installer");

            if (Directory.Exists(GetUpdateAllMarker())) {
                Debug.Log("VRCFury detected UpdateAll marker");
                Directory.Delete(GetUpdateAllMarker());
                await VRCFuryUpdater.UpdateAll();
                return;
            }

            if (Directory.Exists(GetUpdatedMarkerPath())) {
                Debug.Log("VRCFury detected Updated marker");
                Directory.Delete(GetUpdatedMarkerPath());

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
