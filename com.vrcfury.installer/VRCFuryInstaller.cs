using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
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
            Delete(AssetDatabase.GUIDToAssetPath("00b990f230095454f82c345d433841ae"));
            Delete("Assets/VRCFury");
            Delete("Assets/VRCFury-installer");
            Delete("Packages/com.vrcfury.legacyprefabs.tgz");
            Delete("Packages/com.vrcfury.updater.tgz");
            Delete("Packages/com.vrcfury.vrcfury.tgz");
            Delete("Packages/com.vrcfury.legacyprefabs");
            Delete("Packages/com.vrcfury.updater");
            Delete("Packages/com.vrcfury.vrcfury");
            Delete("Packages/com.vrcfury.installer");

            var appRootDir = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(appRootDir + "/Temp/vrcfInstalling");

            Directory.Move(tmpDir, "Packages/com.vrcfury.vrcfury");

            MethodInfo method = typeof(Client).GetMethod("Resolve",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            method.Invoke(null, null);
        });
    }

    private static void Delete(string path) {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (Directory.Exists(path)) Directory.Delete(path, true);
        if (File.Exists(path)) File.Delete(path);
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
