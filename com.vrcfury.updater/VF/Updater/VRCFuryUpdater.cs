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
        
        public static string GetUpdateAllMarker() { 
            return Path.GetDirectoryName(Application.dataPath) + "/~vrcfUpdateAll";
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
            Task.Run(() => ErrorDialogBoundary(async () => {
                string json = await httpClient.GetStringAsync("https://updates.vrcfury.com/updates.json");

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
                    var tgzPath = DownloadTgz(remoteUpdaterPackage.latestUpmTargz);
                    Directory.CreateDirectory(VRCFuryUpdaterStartup.GetUpdatedMarkerPath());
                    await AsyncUtils.AddPackage("file:" + tgzPath);
                    
                }
                
                foreach (var dep in deps) {
                    if (dep.name == "com.vrcfury.updater")
                    if (dep.name.StartsWith("com.unity")) continue;
                    await AsyncUtils.InMainThread(() => {
                        EditorUtility.DisplayDialog("test", dep.name, "ok");
                    });
                }
            }));
        }

        private static async Task<string> DownloadTgz(string url) {
            var tempFile = FileUtil.GetUniqueTempPathInProject() + ".tgz";
            using (var response = await httpClient.GetAsync(url)) {
                using (var fs = new FileStream(tempFile, FileMode.CreateNew)) {
                    await response.Content.CopyToAsync(fs);
                }
            }

            return tempFile;
        }

        private static async Task ErrorDialogBoundary(Func<Task> go) {
            try {
                await go();
            } catch(Exception e) {
                Debug.LogException(e);
                await AsyncUtils.InMainThread(() => {
                    EditorUtility.DisplayDialog(
                        "VRCFury Error",
                        "VRCFury encountered an error.\n\n" + GetGoodCause(e).Message,
                        "Ok"
                    );
                });
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
