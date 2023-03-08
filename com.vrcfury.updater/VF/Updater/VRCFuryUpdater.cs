using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VF.Updater {
    [InitializeOnLoad]
    public class VRCFuryUpdaterStartup { 
        static VRCFuryUpdaterStartup() {
            EditorApplication.delayCall += FirstFrame;
        }

        public static string GetUpdatedMarkerPath() { 
            return Path.GetDirectoryName(Application.dataPath) + "/~vrcfupdated";
        }

        private static void FirstFrame() {
            var updated = false;
            // legacy location
            if (Directory.Exists(Application.dataPath + "/~vrcfupdated")) {
                Directory.Delete(Application.dataPath + "/~vrcfupdated");
                updated = true;
            }
            if (Directory.Exists(GetUpdatedMarkerPath())) {
                Directory.Delete(GetUpdatedMarkerPath());
                updated = true;
            }

            if (!updated) return;

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
                //AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
                //EditorSceneManager.ReloadScene(scene);
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

    public static class VRCFuryUpdater {

        private const string header_name = "Tools/VRCFury/Update";
        private const int header_priority = 1000;
        private const string menu_name = "Tools/VRCFury/Update VRCFury";
        private const int menu_priority = 1001;
        
        [MenuItem(header_name, priority = header_priority)]
        private static void MarkerUpdate() {
        }

        [MenuItem(header_name, true)]
        private static bool MarkerUpdate2() {
            return false;
        }

        [MenuItem(menu_name, priority = menu_priority)]
        public static void Upgrade() {
            ErrorDialogBoundary(() => {
                var client = new WebClient();
                var downloadUrl = "https://gitlab.com/VRCFury/VRCFury/-/archive/main/VRCFury-main.zip";
                var uri = new Uri(downloadUrl);
                client.DownloadFileCompleted += DownloadFileCallback;

                Debug.Log("Downloading VRCFury from " + downloadUrl + " ...");
                client.DownloadFileAsync(uri, "VRCFury.zip");
            });
        }

        private static void DownloadFileCallback(object sender, AsyncCompletedEventArgs e) {
            ErrorDialogBoundary(() => {
                if (e.Cancelled) {
                    throw new Exception("File download was cancelled");
                }
                if (e.Error != null) {
                    throw new Exception(e.Error.ToString());
                }

                Debug.Log("Downloaded");

                Debug.Log("Looking for VRCFury install dir ...");
                var rootPathObj = ScriptableObject.CreateInstance<VRCFuryUpdaterMarker>();
                var rootPathObjPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(rootPathObj));
                var rootDir = Path.GetDirectoryName(rootPathObjPath);
                if (string.IsNullOrEmpty(rootDir)) {
                    throw new Exception("Couldn't find VRCFury install dir");
                }
                if (AssetDatabase.LoadMainAssetAtPath(rootDir + "/VRCFuryUpdaterMarker.cs") == null) {
                    throw new Exception("Found wrong VRCFury install dir? " + rootDir);
                }

                while (AssetDatabase.LoadMainAssetAtPath(rootDir + "/README.md") == null) {
                    if (rootDir.Length < 3) throw new Exception("Failed to find readme");
                    rootDir = Path.GetDirectoryName(rootDir);
                }

                if (AssetDatabase.LoadMainAssetAtPath(rootDir + "/Scripts/Updater/VF/Updater/VRCFuryUpdaterMarker.cs") == null) {
                    throw new Exception("Found wrong VRCFury install dir? " + rootDir);
                }

                Debug.Log(rootDir);

                Debug.Log("Extracting download ...");
                var tmpDir = "VRCFury-Download";
                if (Directory.Exists(tmpDir)) {
                    Directory.Delete(tmpDir, true);
                }
                using (var stream = File.OpenRead("VRCFury.zip")) {
                    using (var archive = new ZipArchive(stream)) {
                        foreach (var entry in archive.Entries) {
                            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                            var outPath = tmpDir+"/"+entry.FullName;
                            var outDir = Path.GetDirectoryName(outPath);
                            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                            using (var entryStream = entry.Open()) {
                                using (var outFile = new FileStream(outPath, FileMode.Create, FileAccess.Write)) {
                                    entryStream.CopyTo(outFile);
                                }
                            }
                        }
                    }
                }
                Debug.Log("Extracted");

                var innerDir = tmpDir+"/VRCFury-main";
                if (!Directory.Exists(innerDir)) {
                    throw new Exception("Missing inner dir from extracted copy?");
                }

                var oldDir = tmpDir + ".old";
                if (Directory.Exists(oldDir)) {
                    Directory.Delete(oldDir, true);
                }
                
                Debug.Log("Saving the project ...");
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();

                Debug.Log("Overwriting VRCFury install ...");

                AssetDatabase.StartAssetEditing();
                try {
                    EditorApplication.LockReloadAssemblies();
                    try {
                        Directory.Move(rootDir, oldDir);
                        Directory.Move(innerDir, rootDir);
                        if (Directory.Exists(oldDir + "/.git")) {
                            Directory.Move(oldDir + "/.git", rootDir + "/.git");
                        }

                        Directory.Delete(tmpDir, true);
                        Directory.Delete(oldDir, true);
                        Directory.CreateDirectory(VRCFuryUpdaterStartup.GetUpdatedMarkerPath());
                    } finally {
                        EditorApplication.UnlockReloadAssemblies();
                    }
                } finally {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CompilationPipeline.RequestScriptCompilation();

                if (!EditorApplication.isCompiling) {
                    throw new Exception("Unity didn't start recompiling scripts on RequestScriptCompilation");
                }

                Debug.Log("Waiting for Unity to recompile scripts ...");
                EditorUtility.DisplayDialog("VRCFury Updater",
                    "Unity is now recompiling VRCFury.\n\nYou will receive another message when the upgrade is complete.",
                    "Ok");
            });
        }
        
        private static bool ErrorDialogBoundary(Action go) {
            try {
                go();
            } catch(Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
                    "VRCFury Error",
                    "VRCFury encountered an error.\n\n" + GetGoodCause(e).Message,
                    "Ok"
                );
                return false;
            }

            return true;
        }
        
        private static Exception GetGoodCause(Exception e) {
            while (e is TargetInvocationException && e.InnerException != null) {
                e = e.InnerException;
            }

            return e;
        }
    }
}
