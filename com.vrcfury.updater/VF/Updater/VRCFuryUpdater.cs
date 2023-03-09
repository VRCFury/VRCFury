using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace VF.Updater {
    public static class VRCFuryUpdater {
        
        private static readonly HttpClient httpClient = new HttpClient();

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

        [Serializable]
        private class Repository {
            public List<Package> packages;
        }

        [Serializable]
        private class Package {
            public string id;
            public string displayName;
            public string latestUpmTargz;
            public string latestVersion;
        }

        [MenuItem(menu_name, priority = menu_priority)]
        public static void Upgrade() {
            UpdateAll();
        }


        private static bool updating = false;
        public static void UpdateAll(bool automated = false) {
            Task.Run(async () => {
                if (updating) {
                    Debug.Log("(VRCFury already has an update in progress)");
                    return;
                }
                updating = true;
                await ErrorDialogBoundary(() => UpdateAllUnsafe(automated));
                updating = false;
            });
        }

        private static async Task UpdateAllUnsafe(bool automated) {
            string json = await DownloadString("https://updates.vrcfury.com/updates.json");

            var repo = JsonUtility.FromJson<Repository>(json);
            if (repo.packages == null) {
                throw new Exception("Failed to fetch packages from update server");
            }

            var deps = await AsyncUtils.ListInstalledPacakges();

            var localUpdaterPackage = deps.FirstOrDefault(d => d.name == "com.vrcfury.updater");
            var remoteUpdaterPackage = repo.packages.FirstOrDefault(p => p.id == "com.vrcfury.updater");

            if (localUpdaterPackage != null
                && remoteUpdaterPackage != null
                && localUpdaterPackage.version != remoteUpdaterPackage.latestVersion
                && remoteUpdaterPackage.latestUpmTargz != null
            ) {
                // An update to the package manager is available
                Debug.Log($"Upgrading updater from {localUpdaterPackage.version} to {remoteUpdaterPackage.latestVersion}");
                if (automated) {
                    throw new Exception("Updater failed to update to new version");
                }

                var remoteName = remoteUpdaterPackage.id;
                var tgzPath = await DownloadTgz(remoteUpdaterPackage.latestUpmTargz);
                await AsyncUtils.AddAndRemovePackages(deps, add: new[]{ (remoteName, tgzPath) });
                Directory.CreateDirectory(await AsyncUtils.InMainThread(VRCFuryUpdaterStartup.GetUpdateAllMarker));
                return;
            }

            var urlsToAdd = deps
                .Select(local => (local, repo.packages.FirstOrDefault(remote => local.name == remote.id)))
                .Where(pair => pair.Item2 != null)
                .Where(pair => pair.Item1.version != pair.Item2.latestVersion)
                .Where(pair => pair.Item2.latestUpmTargz != null);

            var packageFilesToAdd = new List<(string,string)>();
            foreach (var (local,remote) in urlsToAdd) {
                Debug.Log($"Upgrading {local.name} from {local.version} to {remote.latestVersion}");
                var remoteName = remote.id;
                var tgzPath = await DownloadTgz(remote.latestUpmTargz);
                packageFilesToAdd.Add((remoteName, tgzPath));
            }

            if (packageFilesToAdd.Count == 0) {
                await AsyncUtils.DisplayDialog("No new updates are available.");
                return;
            }

            Directory.CreateDirectory(await AsyncUtils.InMainThread(VRCFuryUpdaterStartup.GetUpdatedMarkerPath));
            await AsyncUtils.AddAndRemovePackages(deps, add: packageFilesToAdd);
            
            await AsyncUtils.DisplayDialog(
                "Unity is now recompiling VRCFury.\n\n" +
                "You should receive another message when the upgrade is complete."
            );
        }
        
        private static async Task<string> DownloadString(string url) {
            try {
                using (var response = await httpClient.GetAsync(url)) {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            } catch (Exception e) {
                throw new Exception($"Failed to download {url}\n\n{e.Message}", e);
            }
        }

        private static async Task<string> DownloadTgz(string url) {
            try {
                var tempFile = await AsyncUtils.InMainThread(FileUtil.GetUniqueTempPathInProject) + ".tgz";
                using (var response = await httpClient.GetAsync(url)) {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(tempFile, FileMode.CreateNew)) {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                return tempFile;
            } catch (Exception e) {
                throw new Exception($"Failed to download {url}\n\n{e.Message}", e);
            }
        }

        private static async Task ErrorDialogBoundary(Func<Task> go) {
            try {
                await go();
            } catch(Exception e) {
                Debug.LogException(e);
                await AsyncUtils.DisplayDialog("VRCFury encountered an error.\n\n" + GetGoodCause(e).Message);
            }
        }
        
        private static Exception GetGoodCause(Exception e) {
            while (e is TargetInvocationException && e.InnerException != null) {
                e = e.InnerException;
            }

            return e;
        }
    }
}
