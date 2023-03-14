using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VF.Updater {
    public class PackageActions {
        private static bool alreadyRan = false;
        private List<(string, string)> addPackages = new List<(string, string)>();
        private List<string> removePackages = new List<string>();
        private List<string> deleteDirectories = new List<string>();
        private List<string> createDirectories = new List<string>();
        private bool sceneCloseNeeded = false;

        public void AddPackage(string name, string path) {
            addPackages.Add((name,path));
        }

        public void RemovePackage(string name) {
            removePackages.Add(name);
        }

        public void RemoveDirectory(string path) {
            deleteDirectories.Add(path);
        }
        
        public void CreateDirectory(string path) {
            createDirectories.Add(path);
        }

        public void SceneCloseNeeded() {
            sceneCloseNeeded = true;
        }

        public bool NeedsRun() {
            return addPackages.Count > 0
                   || removePackages.Count > 0
                   || deleteDirectories.Count > 0
                   || createDirectories.Count > 0;
        }

        public async Task Run() {
            if (!NeedsRun()) return;

            // safety in case the updater ran twice somehow
            if (alreadyRan) return;
            alreadyRan = true;
            
            await AsyncUtils.Progress($"Performing package actions ...");

            if (sceneCloseNeeded) {
                await SceneCloser.CloseScenes();
            }
            
            await AsyncUtils.InMainThread(EditorApplication.LockReloadAssemblies);
            try {
                await AsyncUtils.InMainThread(AssetDatabase.StartAssetEditing);
                try {
                    await RunInner();
                } finally {
                    await AsyncUtils.InMainThread(AssetDatabase.StopAssetEditing);
                }
            } finally {
                await AsyncUtils.InMainThread(EditorApplication.UnlockReloadAssemblies);
            }
            
            await AsyncUtils.Progress("Scripts are reloading ...");
            await AsyncUtils.InMainThread(() => {
                Debug.Log("Triggering script import/recompilation");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Triggered");
            });
        }

        private async Task RunInner() {
            // Always remove com.unity.multiplayer-hlapi before doing any package work, because otherwise
            // unity sometimes throws "Copying assembly from Temp/com.unity.multiplayer-hlapi.Runtime.dll
            // to Library/ScriptAssemblies/com.unity.multiplayer-hlapi.Runtime.dll failed and fails to
            // recompile assemblies -_-.
            // Luckily, nobody uses multiplayer-hlapi in a vrchat project anyways.
            var list = await ListInstalledPacakges();
            if (list.Any(p => p.name == "com.unity.multiplayer-hlapi")) {
                await PackageRequest(() => Client.Remove("com.unity.multiplayer-hlapi"));
            }

            foreach (var dir in deleteDirectories) {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }

            foreach (var name in removePackages) {
                await AsyncUtils.Progress($"Removing package {name} ...");
                Debug.Log($"Removing package {name}");
                await PackageRequest(() => Client.Remove(name));
                var savedTgzPath = $"Packages/{name}.tgz";
                if (File.Exists(savedTgzPath)) {
                    Debug.Log($"Deleting {savedTgzPath}");
                    File.Delete(savedTgzPath);
                }
            }

            foreach (var (name,path) in addPackages) {
                await AsyncUtils.Progress($"Importing package {name} ...");
                var savedTgzPath = $"Packages/{name}.tgz";
                if (File.Exists(savedTgzPath)) {
                    Debug.Log($"Deleting {savedTgzPath}");
                    File.Delete(savedTgzPath);
                }
                if (Directory.Exists($"Packages/{name}")) {
                    Debug.Log($"Deleting Packages/{name}");
                    Directory.Delete($"Packages/{name}", true);
                }
                File.Copy(path, savedTgzPath);
                Debug.Log($"Adding package file:{name}.tgz");
                await PackageRequest(() => Client.Add($"file:{name}.tgz"));
            }

            await EnsureVrcfuryEmbedded();

            foreach (var dir in createDirectories) {
                Directory.CreateDirectory(dir);
            }
        }
        
        // Vrcfury packages are all "local" (not embedded), because it makes them read-only which is nice.
        // However, the creator companion can only see embedded packages, so we do this to com.vrcfury.vrcfury only.
        public static async Task EnsureVrcfuryEmbedded() {
            foreach (var local in await ListInstalledPacakges()) {
                if (local.name == "com.vrcfury.vrcfury" && local.source == PackageSource.LocalTarball) {
                    Debug.Log($"Embedding package {local.name}");
                    await PackageRequest(() => Client.Embed(local.name));
                }
            }
        }
        
        public static async Task<PackageCollection> ListInstalledPacakges() {
            return await PackageRequest(() => Client.List(true, false));
        }
        
        private static async Task<T> PackageRequest<T>(Func<Request<T>> requestProvider) {
            var request = await AsyncUtils.InMainThread(requestProvider);
            await PackageRequest(request);
            return request.Result;
        }
        private static async Task PackageRequest(Func<Request> requestProvider) {
            var request = await AsyncUtils.InMainThread(requestProvider);
            await PackageRequest(request);
        }
        private static Task PackageRequest(Request request) {
            var promise = new TaskCompletionSource<object>();
            void Check() {
                if (!request.IsCompleted) {
                    EditorApplication.delayCall += Check;
                    return;
                }
                if (request.Status == StatusCode.Failure) {
                    promise.SetException(new Exception(request.Error.message));
                    return;
                }
                promise.SetResult(null);
            }
            EditorApplication.delayCall += Check;
            return promise.Task;
        }
    }
}