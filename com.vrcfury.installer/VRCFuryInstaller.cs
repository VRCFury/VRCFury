using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

[InitializeOnLoad]
public class VRCFuryInstaller { 
    
    private static readonly HttpClient HttpClient = new HttpClient();
    
    static VRCFuryInstaller() {
        Task.Run(async () => {
            try {
                await InstallUnsafe();
            } catch(Exception e) {
                Debug.LogException(e);
                await DisplayDialog(
                    "VRCFury encountered an error while installing." +
                    " If the issue repeats, try re-downloading from https://vrcfury.com/download or ask on the" +
                    " discord: https://vrcfury.com/discord\n\n" +
                    e.Message + "\nCheck the unity console for details.");
            }
        });
    }

    private static async Task InstallUnsafe() {
        if (Directory.Exists("Temp/vrcfDoNotInstall")) {
            return;
        }
        
        var restarting = await InMainThread(() => {
            var changed = false;
            changed |= Delete("Packages/com.vrcfury.vrcfury.tgz");
            changed |= Delete("Packages/com.vrcfury.vrcfury");
            changed |= CleanManifest(true);
            RefreshPackages();
            return changed;
        });
        if (restarting) {
            // Unity will probably unload us during this pause, but that's fine, we'll just start over.
            // We need to make sure unity has totally forgotten about com.vrcfury.vrcfury before we install
            // the new one, otherwise it will delete our new com.vrcfury.vrcfury folder when it cleans up
            // the upm package.
            await Task.Delay(10000);
        }

        var url = "https://vrcfury.com/downloadRawZip";
        var tempFile = await InMainThread(FileUtil.GetUniqueTempPathInProject) + ".zip";
        try {
            using (var response = await HttpClient.GetAsync(url)) {
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(tempFile, FileMode.CreateNew)) {
                    await response.Content.CopyToAsync(fs);
                }
            }
        } catch (Exception e) {
            throw new Exception($"Failed to download {url}\n{e.Message}", e);
        }

        var tmpDir = await InMainThread(FileUtil.GetUniqueTempPathInProject);
        using (var stream = File.OpenRead(tempFile)) {
            using (var archive = new ZipArchive(stream)) {
                foreach (var entry in archive.Entries) {
                    if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                    var outPath = tmpDir+"/"+entry.FullName;
                    var outDir = Path.GetDirectoryName(outPath);
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    using (var entryStream = entry.Open()) {
                        using (var outFile = new FileStream(outPath, FileMode.Create, FileAccess.Write)) {
                            await entryStream.CopyToAsync(outFile);
                        }
                    }
                }
            }
        }

        await InMainThread(() => {
            var appRootDir = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(appRootDir + "/Temp/vrcfInstalling");

            Debug.Log($"Moving {tmpDir} to Packages/com.vrcfury.vrcfury");
            Directory.Move(tmpDir, "Packages/com.vrcfury.vrcfury");

            CleanManifest(false);
            Delete(AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            Delete("Assets/VRCFury");
            Delete("Assets/VRCFury-installer");
            Delete("Packages/com.vrcfury.legacyprefabs.tgz");
            Delete("Packages/com.vrcfury.legacyprefabs");
            Delete("Packages/com.vrcfury.updater.tgz");
            Delete("Packages/com.vrcfury.updater");
            Delete("Packages/com.vrcfury.installer");

            RefreshPackages();
        });
    }

    private static void RefreshPackages() {
        MethodInfo method = typeof(Client).GetMethod("Resolve",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        method.Invoke(null, null);
    }

    private static bool CleanManifest(bool mainOnly) {
        var manifestPath = "Packages/manifest.json";
        if (!File.Exists(manifestPath)) return false;
        var lines = File.ReadLines(manifestPath).ToArray();
        bool ShouldRemoveLine(string line) {
            return line.Contains("com.vrcfury.") && (!mainOnly || line.Contains("com.vrcfury.vrcfury"));
        }
        var linesToKeep = lines.Where(l => !ShouldRemoveLine(l)).ToArray();
        if (lines.Length == linesToKeep.Length) return false;
        var tempManifestPath = FileUtil.GetUniqueTempPathInProject();
        File.WriteAllLines(tempManifestPath, linesToKeep);
        File.Delete(manifestPath);
        File.Move(tempManifestPath, manifestPath);
        return true;
    }

    private static bool Delete(string path) {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (Directory.Exists(path)) {
            Debug.Log("Deleting directory: " + path);
            Directory.Delete(path, true);
            return true;
        }
        if (File.Exists(path)) {
            Debug.Log("Deleting file: " + path);
            File.Delete(path);
            return true;
        }

        return false;
    }

    private static async Task DisplayDialog(string msg) {
        await InMainThread(() => {
            EditorUtility.DisplayDialog(
                "VRCFury Installer",
                msg,
                "Ok"
            );
        });
    }
    
    private static async Task InMainThread(Action fun) {
        await InMainThread<object>(() => { fun(); return null; });
    }
    private static Task<T> InMainThread<T>(Func<T> fun) {
        var promise = new TaskCompletionSource<T>();
        void Callback() {
            try {
                promise.SetResult(fun());
            } catch (Exception e) {
                promise.SetException(e);
            }
        }
        EditorApplication.delayCall += Callback;

        return promise.Task;
    }
}
