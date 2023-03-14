using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

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

        public static async Task Progress(string msg) {
            await InMainThread(() => {
                Debug.Log("VRCFury Update: " + msg);
            });
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
                await DisplayDialog(
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
    }
}
