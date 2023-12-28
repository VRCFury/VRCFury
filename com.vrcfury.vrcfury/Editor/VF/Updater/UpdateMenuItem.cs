using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;
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
            Task.Run(() => VRCFExceptionUtils.ErrorDialogBoundaryAsync(UpgradeUnsafe));
        }

        private static async Task UpgradeUnsafe() {
            var vpmManifest = "Packages/vpm-manifest.json";
            if (File.Exists(vpmManifest) && File.ReadLines(vpmManifest).Any(line => line.Contains("vrcfury"))) {
                throw new Exception(
                    "VRCFury was installed using the VRChat Creator Companion. " +
                    "Please update VRCFury in the Creator Companion app, in the Manage Project section.");
            }

            if (!IsVrcfuryALocalPackage()) {
                throw new Exception(
                    "VRCFury is not installed as a local package, and thus cannot update itself.");
            }
            
            Log("Downloading installer ...");

            var url = "https://vrcfury.com/installer";
            var tempFile = await AsyncUtils.InMainThread(FileUtil.GetUniqueTempPathInProject) + ".unitypackage";
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

            await AsyncUtils.InMainThread(() => {
                Log("Importing installer ...");
                AssetDatabase.ImportPackage(tempFile, false);
            });
        }

        private static void Log(string message) {
            Debug.Log("VRCFury Updater > " + message);
        }
    }
}
