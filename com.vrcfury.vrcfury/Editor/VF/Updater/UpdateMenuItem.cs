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
using VF.Utils;

namespace VF.Updater {
    internal static class UpdateMenuItem {
        
        private static readonly HttpClient HttpClient = new HttpClient();

        private static readonly Lazy<bool> shouldShow = new Lazy<bool>(() => {
            var vpmManifest = "Packages/vpm-manifest.json";
            if (File.Exists(vpmManifest) && File.ReadLines(vpmManifest).Any(line => line.Contains("vrcfury"))) {
                // Installed using VCC
                return false;
            }
            var unityManifest = "Packages/manifest.json";
            if (File.Exists(unityManifest) && File.ReadLines(unityManifest).Any(line => line.Contains("vrcfury"))) {
                // Installed from disk for dev
                return false;
            }
            return true;
        });
        public static bool ShouldShow() {
            return shouldShow.Value;
        }

        public static void Upgrade() {
            Task.Run(() => VRCFExceptionUtils.ErrorDialogBoundaryAsync(UpgradeUnsafe));
        }

        private static async Task UpgradeUnsafe() {
            if (!ShouldShow()) {
                throw new Exception(
                    "VRCFury was installed using the VRChat Creator Companion. " +
                    "Please update VRCFury in the Creator Companion app, in the Manage Project section.");
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
