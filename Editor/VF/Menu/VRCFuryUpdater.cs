using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using VF.Inspector;

namespace VF.Menu {
    
    [InitializeOnLoad]
    public class VRCFuryUpdaterStartup {
        static VRCFuryUpdaterStartup() {
            if (Directory.Exists(Application.dataPath + "/~vrcfupdated")) {
                Directory.Delete(Application.dataPath + "/~vrcfupdated");
                EditorUtility.ClearProgressBar();
                Debug.Log("Upgrade complete");
                EditorUtility.DisplayDialog(
                    "VRCFury Updater",
                    "VRCFury has been updated. Unity may be frozen for a bit as it reloads.",
                    "Ok"
                );
            }
        }
    }

    public static class VRCFuryUpdater {

    public static void Upgrade() {
        WithErrorPopup(() => {
            var client = new WebClient();
            var downloadUrl = "https://gitlab.com/VRCFury/VRCFury/-/archive/main/VRCFury-main.zip";
            var uri = new Uri(downloadUrl);
            client.DownloadFileCompleted += DownloadFileCallback;

            Debug.Log("Downloading VRCFury from " + downloadUrl + " ...");
            client.DownloadFileAsync(uri, "VRCFury.zip");
        });
    }

    private static void DownloadFileCallback(object sender, AsyncCompletedEventArgs e) {
        WithErrorPopup(() => {
            if (e.Cancelled) {
                throw new Exception("File download was cancelled");
            }
            if (e.Error != null) {
                throw new Exception(e.Error.ToString());
            }

            Debug.Log("Downloaded");

            Debug.Log("Looking for VRCFury install dir ...");
            var rootPathObj = ScriptableObject.CreateInstance<VRCFuryEditor>();
            var rootPathObjPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(rootPathObj));
            var rootDir = Path.GetDirectoryName(rootPathObjPath);
            if (string.IsNullOrEmpty(rootDir)) {
                throw new Exception("Couldn't find VRCFury install dir");
            }
            if (AssetDatabase.LoadMainAssetAtPath(rootDir + "/VRCFuryEditor.cs") == null) {
                throw new Exception("Found wrong VRCFury install dir? " + rootDir);
            }

            while (AssetDatabase.LoadMainAssetAtPath(rootDir + "/README.md") == null) {
                if (rootDir.Length < 3) throw new Exception("Failed to find readme");
                rootDir = Path.GetDirectoryName(rootDir);
            }

            if (AssetDatabase.LoadMainAssetAtPath(rootDir + "/Runtime/VRCFury.asmdef") == null) {
                throw new Exception("Failed to find asmdef");
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

            Debug.Log("Overwriting VRCFury install ...");

            try {
                AssetDatabase.StartAssetEditing();
                try {
                    EditorApplication.LockReloadAssemblies();
                    Directory.Move(rootDir, oldDir);
                    Directory.Move(innerDir, rootDir);
                    if (Directory.Exists(oldDir + "/.git")) {
                        Directory.Move(oldDir + "/.git", rootDir + "/.git");
                    }

                    Directory.Delete(tmpDir, true);
                    Directory.Delete(oldDir, true);
                    Directory.CreateDirectory(Application.dataPath + "/~vrcfupdated");
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
            EditorUtility.DisplayDialog("VRCFury Updater", "Unity is now recompiling VRCFury. You will receive another message when the upgrade is complete.", "Ok");
        });
    }

    private static void WithErrorPopup(Action stuff) {
        try {
            stuff();
        } catch(Exception e) {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VRCFury Updater", "An error occurred. Check the unity console.", "Ok");
        }
    }
}

}
