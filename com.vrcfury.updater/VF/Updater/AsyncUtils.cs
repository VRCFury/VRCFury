using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditorInternal;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

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
        
        public static async Task AddAndRemovePackages(IList<(string, string)> add = null, IList<string> remove = null) {
            await PreventReload(async () => {
                // Always remove com.unity.multiplayer-hlapi before doing any package work, because otherwise
                // unity sometimes throws "Copying assembly from Temp/com.unity.multiplayer-hlapi.Runtime.dll
                // to Library/ScriptAssemblies/com.unity.multiplayer-hlapi.Runtime.dll failed and fails to
                // recompile assemblies -_-.
                // Luckily, nobody uses multiplayer-hlapi in a vrchat project anyways.
                var list = await ListInstalledPacakges();
                if (list.Any(p => p.name == "com.unity.multiplayer-hlapi")) {
                    await PackageRequest(() => Client.Remove("com.unity.multiplayer-hlapi"));
                }

                if (remove != null) {
                    foreach (var name in remove) {
                        Debug.Log($"Removing package {name}");
                        await PackageRequest(() => Client.Remove(name));
                        var savedTgzPath = $"Packages/{name}.tgz";
                        if (File.Exists(savedTgzPath)) {
                            Debug.Log($"Deleting {savedTgzPath}");
                            File.Delete(savedTgzPath);
                        }
                    }
                }

                if (add != null) {
                    foreach (var (name,path) in add) {
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
                }

                await EnsureVrcfuryEmbedded();
            });
            await TriggerReload();
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

        public static async Task ErrorDialogBoundary(Func<Task> go) {
            try {
                await go();
            } catch(Exception e) {
                Debug.LogException(e);
                await AsyncUtils.DisplayDialog(
                    "VRCFury encountered an error while installing/updating." +
                    " You may need to Tools -> VRCFury -> Update VRCFury again. If the issue repeats," +
                    " try re-downloading from https://vrcfury.com/download or ask on the" +
                    " discord: https://vrcfury.com/discord" +
                    "\n\n" + GetGoodCause(e).Message);
            }
        }

        private static Exception GetGoodCause(Exception e) {
            while (e is TargetInvocationException && e.InnerException != null) {
                e = e.InnerException;
            }

            return e;
        }

        private static int preventReloadCount = 0;
        private static bool triggerReloadOnUnlock = false;
        public async static Task PreventReload(Func<Task> act) {
            try {
                await InMainThread(() => {
                    preventReloadCount++;
                    //Debug.Log($"{Assembly.GetExecutingAssembly().GetName().Name} Reload counter: {preventReloadCount}");
                    if (preventReloadCount == 1) EditorApplication.LockReloadAssemblies();
                });
                await act();
            } finally {
                await InMainThread(() => {
                    preventReloadCount--;
                    //Debug.Log($"{Assembly.GetExecutingAssembly().GetName().Name} Reload counter: {preventReloadCount}");
                    if (preventReloadCount == 0) {
                        EditorApplication.UnlockReloadAssemblies();
                        if (triggerReloadOnUnlock) {
                            triggerReloadOnUnlock = false;
                            TriggerReloadNow();
                        }
                    }
                });
            }
        }
        public static async Task TriggerReload() {
            if (preventReloadCount == 0) {
                await InMainThread(TriggerReloadNow);
            } else {
                triggerReloadOnUnlock = true;
            }
        }
        private static void TriggerReloadNow() {
            Debug.Log("Triggering script import/recompilation");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Triggered");
        }
    }
}
