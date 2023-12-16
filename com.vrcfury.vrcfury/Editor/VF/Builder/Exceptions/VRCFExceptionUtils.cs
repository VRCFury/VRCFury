using System;
using System.Collections.Generic;
using System.Linq;
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
                
                var sneaky = SneakyException.GetFromStack(e);
                if (sneaky != null) {
                    EditorUtility.DisplayDialog(
                        "Avatar Error",
                        sneaky.Message,
                        "Ok"
                    );
                } else {
                    var message = GetGoodCause(e).Message.Trim();
                    var closestLine = GetClosestVrcfuryLine(e);
                    var output = new List<string>();
                    output.Add("VRCFury encountered an error.");
                    if (!string.IsNullOrWhiteSpace(message)) output.Add(message);
                    if (!string.IsNullOrWhiteSpace(closestLine)) output.Add($"({e.GetBaseException().GetType().Name} {closestLine})");
                    EditorUtility.DisplayDialog(
                        "VRCFury Error",
                        string.Join("\n\n", output),
                        "Ok"
                    );
                }

                return false;
            }

            return true;
        }

        private static string GetClosestVrcfuryLine(Exception e) {
            var causes = new List<Exception>();
            var current = e;
            while (current != null) {
                causes.Add(current);
                current = current.InnerException;
            }
            causes.Reverse();
            foreach (var cause in causes) {
                foreach (var line in cause.StackTrace.Split("\n")) {
                    if (line.Contains("VF")) {
                        return line.Trim();
                    }
                }
            }
            return "";
        }
    }
}
