using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;

namespace VF.Utils {
    [InitializeOnLoad]
    public class ErrorCatcher {
        private static readonly List<string> errors = new List<string>();
        static ErrorCatcher() {
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
        }
        
        public static IList<string> Errors => errors;
    }
}
