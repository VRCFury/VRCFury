using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VF.Updater {
    public static class VRCFuryUpdater {
        
        private static readonly HttpClient httpClient = new HttpClient();

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

        public static async Task AddUpdateActions(bool failIfUpdaterNeedsUpdated, PackageActions actions) {
            try {
                await AddUpdateActionsUnsafe(failIfUpdaterNeedsUpdated, actions);
            } finally {
                await AsyncUtils.InMainThread(EditorUtility.ClearProgressBar);
            }
        }

        private static async Task AddUpdateActionsUnsafe(bool failIfUpdaterNeedsUpdated, PackageActions actions) {
            if (await AsyncUtils.InMainThread(() => EditorApplication.isPlaying)) {
                throw new Exception("VRCFury cannot update in play mode");
            }

            Debug.Log("Downloading update manifest...");
            await AsyncUtils.Progress("Checking for updates ...");
            string json = await DownloadString("https://updates.vrcfury.com/updates.json?_=" + DateTime.Now);

            var repo = JsonUtility.FromJson<Repository>(json);
            if (repo.packages == null) {
                throw new Exception("Failed to fetch packages from update server");
            }
            Debug.Log($"Update manifest includes {repo.packages.Count} packages");
            
            await AsyncUtils.Progress("Downloading updated packages ...");

            var deps = await actions.ListInstalledPacakges();

            var localUpdaterPackage = deps.FirstOrDefault(d => d.name == "com.vrcfury.updater");
            var remoteUpdaterPackage = repo.packages.FirstOrDefault(p => p.id == "com.vrcfury.updater");

            if (remoteUpdaterPackage != null
                && remoteUpdaterPackage.latestUpmTargz != null
                && (localUpdaterPackage == null || localUpdaterPackage.version != remoteUpdaterPackage.latestVersion)
            ) {
                // An update to the package manager is available
                Debug.Log($"Upgrading updater from {localUpdaterPackage?.version} to {remoteUpdaterPackage.latestVersion}");
                if (failIfUpdaterNeedsUpdated) {
                    throw new Exception("Updater failed to update to new version");
                }

                var remoteName = remoteUpdaterPackage.id;
                var tgzPath = await DownloadTgz(remoteUpdaterPackage.latestUpmTargz);
                actions.CreateMarker(await Markers.UpdaterJustUpdated());
                actions.AddPackage(remoteName, tgzPath);
                await actions.Run();
                return;
            }

            var urlsToAdd = repo.packages
                .Where(remote => remote.latestUpmTargz != null)
                .Select(remote => (deps.FirstOrDefault(d => d.name == remote.id), remote))
                .Where(pair => {
                    var (local, remote) = pair;
                    if (local == null && remote.id == "com.vrcfury.vrcfury") return true;
                    if (local == null && remote.id == "com.vrcfury.legacyprefabs") return true;
                    if (local != null && local.version != remote.latestVersion) return true;
                    return false;
                }).ToList();

            if (urlsToAdd.Count > 0) {
                actions.SceneCloseNeeded();
                foreach (var (local,remote) in urlsToAdd) {
                    Debug.Log($"Upgrading {remote.id} from {local?.version} to {remote.latestVersion}");
                    var remoteName = remote.id;
                    var tgzPath = await DownloadTgz(remote.latestUpmTargz);
                    actions.AddPackage(remoteName, tgzPath);
                }
            }
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
    }
}
