using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace VF.Updater {
    public class AsyncUtils {
        public static async Task<PackageCollection> ListInstalledPacakges() {
            return await PackageRequest(Client.List(true, false));
        }
        
        public static async Task AddAndRemovePackages(IList<string> add = null, IList<string> remove = null) {
            try {
                EditorApplication.LockReloadAssemblies();
                if (remove != null) {
                    foreach (var p in remove) {
                        await PackageRequest(Client.Remove(p));
                    }
                }
                if (add != null) {
                    foreach (var p in add) {
                        await PackageRequest(Client.Add(p));
                    }
                }
            } finally {
                EditorApplication.UnlockReloadAssemblies();
            }
            
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CompilationPipeline.RequestScriptCompilation();
        }
        
        private static async Task<T> PackageRequest<T>(Request<T> request) {
            await PackageRequest((Request)request);
            return request.Result;
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

        public static Task InMainThread(Action fun) {
            var promise = new TaskCompletionSource<object>();
            EditorApplication.delayCall += () => {
                fun();
                promise.SetResult(null);
            };
            return promise.Task;
        }
    }
}