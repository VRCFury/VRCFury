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
using UnityEngine.WSA;

namespace VF.Updater {
    public static class AsyncUtils {
        public static async Task DisplayDialog(string msg) {
            await InMainThread(() => {
                EditorUtility.DisplayDialog(
                    "VRCFury Updater",
                    msg,
                    "Ok"
                );
            });
        }
        
        public static async Task<PackageCollection> ListInstalledPacakges() {
            return await PackageRequest(() => Client.List(true, false));
        }
        
        public static async Task AddAndRemovePackages(IList<(string,string)> add = null, IList<string> remove = null) {
            var existing = await ListInstalledPacakges();

            var actualAdd = new Dictionary<string, string>();
            var actualRemove = new HashSet<string>();
            if (remove != null) actualRemove.UnionWith(remove);
            if (add != null) {
                foreach (var (name, path) in add) {
                    var exists = existing.FirstOrDefault(other => other.name == name);
                    if (exists != null && exists.source == PackageSource.Embedded) {
                        actualRemove.Add(name);
                    }
                    actualAdd[name] = path;
                }
            }

            try {
                await InMainThread(EditorApplication.LockReloadAssemblies);
                foreach (var name in actualRemove) {
                    var exists = existing.FirstOrDefault(other => other.name == name);
                    var deleteFile =
                        exists != null
                        && exists.source == PackageSource.LocalTarball
                        && exists.resolvedPath.Contains(VRCFuryUpdaterStartup.GetAppRootDir());
                    var deletePath = exists.resolvedPath;
                    await PackageRequest(() => Client.Remove(name));
                    // TODO: Delete the old package path if it's a tgz and it's inside the project directory
                    // DO IT HERE
                    Debug.Log("Delete " + deleteFile + " " + deletePath);
                }
                foreach (var (name,path) in actualAdd.Select(x => (x.Key,x.Value))) {
                    await PackageRequest(() => Client.Add("file:" + Path.GetFullPath(path)));
                    if (name == "com.vrcfury.vrcfury") {
                        // This makes the creator companion happy, since it can only "see" embedded
                        // packages.
                        await PackageRequest(() => Client.Embed(name));
                    }
                }
            } finally {
                await InMainThread(EditorApplication.UnlockReloadAssemblies);
            }

            await InMainThread(() => {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CompilationPipeline.RequestScriptCompilation();
            });
        }

        private static async Task<T> PackageRequest<T>(Func<Request<T>> requestProvider) {
            var request = await InMainThread(requestProvider);
            await PackageRequest(request);
            return request.Result;
        }
        private static async Task PackageRequest(Func<Request> requestProvider) {
            var request = await InMainThread(requestProvider);
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

        public static async Task InMainThread(Action fun) {
            await InMainThread<object>(() => { fun(); return null; });
        }
        public static Task<T> InMainThread<T>(Func<T> fun) {
            var promise = new TaskCompletionSource<T>();
            EditorApplication.delayCall += () => {
                try {
                    promise.SetResult(fun());
                } catch (Exception e) {
                    promise.SetException(e);
                }
            };
            return promise.Task;
        }
    }
}
