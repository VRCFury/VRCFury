using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;

namespace VF.Updater {
    public class AsyncUtils {
        public static Task<PackageCollection> ListInstalledPacakges() {
            var promise = new TaskCompletionSource<PackageCollection>();
            EditorApplication.delayCall += () => {
                var currentDeps = Client.List(true, false);

                void Check() {
                    if (!currentDeps.IsCompleted) {
                        EditorApplication.delayCall += Check;
                        return;
                    }
                    if (currentDeps.Status == StatusCode.Failure) {
                        promise.SetException(new Exception(currentDeps.Error.message));
                        return;
                    }
                    promise.SetResult(currentDeps.Result);
                }

                Check();
            };
            return promise.Task;
        }
        
        public static Task AddPackage(string id) {
            var promise = new TaskCompletionSource<object>();
            EditorApplication.delayCall += () => {
                var request = Client.Add(id);

                void Check() {
                    if (!request.IsCompleted) {
                        EditorApplication.delayCall += Check;
                        return;
                    }
                    if (request.Status == StatusCode.Failure) {
                        promise.SetException(new Exception(request.Error.message));
                        return;
                    }
                    promise.SetResult(request.Result);
                }

                Check();
            };
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