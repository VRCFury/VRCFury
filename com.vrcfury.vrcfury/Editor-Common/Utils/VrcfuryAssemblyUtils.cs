using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;

namespace VF.Utils {
    internal static class VrcfuryAssemblyUtils {
        private static readonly Lazy<IReadOnlyList<(Assembly assembly, string asmdefPath)>> Cache = new(() =>
            ReflectionUtils.GetLoadedAssemblies()
                .Select(assembly => (
                    assembly,
                    asmdefPath: CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.GetName().Name)
                ))
                .Where(entry => entry.asmdefPath?.IndexOf("com.vrcfury.vrcfury", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray()
        );

        public static IReadOnlyList<(Assembly assembly, string asmdefPath)> Assemblies => Cache.Value;
    }
}
