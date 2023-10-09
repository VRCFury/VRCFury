using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VF.Builder.Exceptions {
    public static class VRCFExceptionUtils {
        public static Exception GetGoodCause(Exception e) {
            while (e is TargetInvocationException && e.InnerException != null) {
                e = e.InnerException;
            }

            return e;
        }
        
        public static async Task ErrorDialogBoundaryAsync(Func<Task> go) {
            try {
                await go();
            } catch(Exception e) {
                Debug.LogException(e);
                await AsyncUtils.DisplayDialog($"VRCFury encountered an error.\n\n{GetGoodCause(e).Message}");
            }
        }

        public static bool ErrorDialogBoundary(Action go) {
            try {
                go();
            } catch(Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
                    "VRCFury Error",
                    $"VRCFury encountered an error.\n\n{GetGoodCause(e).Message}",
                    "Ok"
                );
                return false;
            }

            return true;
        }
    }
}
