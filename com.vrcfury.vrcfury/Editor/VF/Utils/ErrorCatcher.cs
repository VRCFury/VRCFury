using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace VF.Utils {
    internal static class ErrorCatcher {
        private static readonly List<string> errors = new List<string>();
        
        [InitializeOnLoadMethod]
        private static void Init() {
            CompilationPipeline.compilationStarted += context => {
                errors.Clear();
            };
            CompilationPipeline.assemblyCompilationFinished += (s, messages) => {
                foreach (var m in messages) {
                    if (m.type == CompilerMessageType.Error) {
                        errors.Add(m.message);
                    }
                }
            };

            EditorApplication.delayCall += () => {
                if (EditorUtility.scriptCompilationFailed && errors.Count == 0) {
                    var key = "com.vrcfury.lastScriptReloadTrigger";
                    var lastReload = SessionState.GetFloat(key, -999);
                    var now = EditorApplication.timeSinceStartup;
                    if (now - lastReload > 30) {
                        Debug.Log("VRCFury is triggering scripts to reload so it can read the compilation errors");
                        SessionState.SetFloat(key, (float)now);
                        CompilationPipeline.RequestScriptCompilation();
                    }
                }
            };
        }
        
        public static IList<string> Errors => errors;
    }
}
