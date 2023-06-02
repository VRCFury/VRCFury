using System;
using System.Threading.Tasks;
using UnityEditor;

namespace VF {
    public class AsyncUtils {
        public static async Task InMainThread(Action fun) {
            await InMainThread<object>(() => { fun(); return null; });
        }
        public static Task<T> InMainThread<T>(Func<T> fun) {
            var promise = new TaskCompletionSource<T>();
            void Callback() {
                try {
                    promise.SetResult(fun());
                } catch (Exception e) {
                    promise.SetException(e);
                }
            }
            EditorApplication.delayCall += Callback;

            return promise.Task;
        }
        public static async Task DisplayDialog(string msg) {
            await InMainThread(() => {
                EditorUtility.DisplayDialog(
                    "VRCFury",
                    msg,
                    "Ok"
                );
            });
        }
    }
}
