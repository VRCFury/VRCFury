using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Updater {
    [InitializeOnLoad]
    public class VRCFuryUpdaterStartup { 
        static VRCFuryUpdaterStartup() {
            EditorApplication.delayCall += FirstFrame;
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
        
        public static string GetInstallLegacyMarker() { 
            return GetAppRootDir() + "/Library/vrcfInstallLegacy";
        }

        private static void FirstFrame() {

            if (Directory.Exists(GetUpdateAllMarker())) {
                Debug.Log("VRCFury detected UpdateAll marker");
                Directory.Delete(GetUpdateAllMarker());
                VRCFuryUpdater.UpdateAll();
                return;
            }

            if (Directory.Exists(GetUpdatedMarkerPath())) {
                Debug.Log("VRCFury detected Updated marker");
                Directory.Delete(GetUpdatedMarkerPath());

                // We need to reload scenes. If we do not, any serialized data with a changed type will be deserialized as "null"
                // This is especially common for fields that we change from a non-guid type to a guid type, like
                // AnimationClip to GuidAnimationClip.
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
            }
        }
    }
}
