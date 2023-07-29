using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Updater {
    public class UpdateMenuItem {
        private const string updateName = "Tools/VRCFury/Update VRCFury";
        private const int updatePriority = 1000;
        private const string removeName = "Tools/VRCFury/Uninstall VRCFury";
        private const int removePriority = 1001;
        
        private static readonly HttpClient HttpClient = new HttpClient();

        public static bool IsVrcfuryALocalPackage() {
            return Directory.Exists("Packages/com.vrcfury.vrcfury") &&
                   Path.GetFullPath("Packages/com.vrcfury.vrcfury").StartsWith(Path.GetFullPath("Packages"));
        }

        [MenuItem(updateName, priority = updatePriority)]
        public static void Upgrade() {
            Task.Run(() => VRCFExceptionUtils.ErrorDialogBoundaryAsync(async () => {
                if (!IsVrcfuryALocalPackage()) {
                    throw new Exception(
                        "VRCFury is not installed as a local package, and thus cannot update itself.");
                }
                
                var url = "https://vrcfury.com/downloadRawZip";
                var tempFile = await AsyncUtils.InMainThread(FileUtil.GetUniqueTempPathInProject) + ".zip";
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

                var tmpDir = await AsyncUtils.InMainThread(FileUtil.GetUniqueTempPathInProject);
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

                await AsyncUtils.InMainThread(() => {
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

                    TmpFilePackage.ReresolvePackages();
                });
            }));
        }
        
        private static void Delete(string path) {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Directory.Exists(path)) Directory.Delete(path, true);
            if (File.Exists(path)) File.Delete(path);
        }

/*        
        [MenuItem(removeName, priority = removePriority)]
        public static void Remove() {
            Task.Run(() => VRCFExceptionUtils.ErrorDialogBoundaryAsync(async () => {
                var actions = new PackageActions(msg => Debug.Log($"VRCFury Remover: {msg}"));
                var list = await actions.ListInstalledPacakges();
                var removeIds = list
                    .Select(p => p.name)
                    .Where(name => name.StartsWith("com.vrcfury"))
                    .ToArray();
                if (removeIds.Length == 0) {
                    throw new Exception("VRCFury packages not found");
                }
                
                var doIt = await AsyncUtils.InMainThread(() => EditorUtility.DisplayDialog("VRCFury",
                    "Uninstall VRCFury? Beware that all VRCFury scripts in your avatar will break.\n\nThe following packages will be removed:\n" + string.Join("\n", removeIds),
                    "Uninstall",
                    "Cancel"));
                if (!doIt) return;

                foreach (var id in removeIds) {
                    actions.RemovePackage(id);
                }

                await actions.Run();
            }));
        }
        */
    }
}
