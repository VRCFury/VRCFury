using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

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
        
        public static async Task AddAndRemovePackages(PackageCollection existingLocal, IList<string> add = null, IList<string> remove = null) {
            try {
                await InMainThread(EditorApplication.LockReloadAssemblies);
                if (remove != null) {
                    foreach (var p in remove) {
                        await PackageRequest(() => Client.Remove(p));
                    }
                }
                if (add != null) {
                    foreach (var p in add) {
                        var exists = existingLocal.FirstOrDefault(other => other.name == p);
                        if (exists != null && exists.source == PackageSource.Embedded) {
                            await PackageRequest(() => Client.Remove(p));
                        }
                    }
                    foreach (var p in add) {
                        await PackageRequest(() => Client.Add("file:" + Path.GetFullPath(p)));
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
