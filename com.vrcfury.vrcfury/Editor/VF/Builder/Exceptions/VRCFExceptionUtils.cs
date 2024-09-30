using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using VF.Inspector;
using VF.Utils;
using Debug = UnityEngine.Debug;

namespace VF.Builder.Exceptions {
    internal static class VRCFExceptionUtils {
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
                await AsyncUtils.InMainThread(() => {
                    DisplayErrorPopup(e);
                });
            }
        }

        public static bool ErrorDialogBoundary(Action go) {
            try {
                go();
            } catch(Exception e) {
                DisplayErrorPopup(e);
                return false;
            }

            return true;
        }

        private static void DisplayErrorPopup(Exception e) {
            Debug.LogException(e);
                
            var sneaky = SneakyException.GetFromStack(e);
            if (sneaky != null) {
                EditorUtility.DisplayDialog(
                    "Avatar Error",
                    sneaky.Message,
                    "Ok"
                );
            } else {
                var message = GetGoodCause(e).Message.Trim();
                
                var output = "VRCFury encountered an error.";
                if (!string.IsNullOrWhiteSpace(message)) output += "\n\n" + message;

                DialogUtils.DisplayDialog(
                    "VRCFury Error",
                    output,
                    "Ok",
                    ex: e
                );
            }
        }

        public static string GetClosestVrcfuryLine(Exception e) {
            var causes = new List<Exception>();
            var current = e;
            while (current != null) {
                causes.Add(current);
                current = current.InnerException;
            }
            causes.Reverse();
            foreach (var cause in causes) {
                var stack = new System.Diagnostics.StackTrace(cause, true);
                var frames = stack.GetFrames();
                if (frames == null) continue;
                foreach (var frame in frames) {
                    if (frame.GetMethod()?.DeclaringType?.FullName?.StartsWith("VF") ?? false) {
                        var filename = Path.GetFileName(frame.GetFileName());
                        return
                            frame.GetMethod().Name +
                            " at " +
                            $"{filename}:{frame.GetFileLineNumber()}:{frame.GetFileColumnNumber()}";
                    }
                }
            }
            return "";
        }
    }
}
