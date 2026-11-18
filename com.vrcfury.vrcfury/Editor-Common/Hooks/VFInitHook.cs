using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEditor;
using VF.Menu;
using Debug = UnityEngine.Debug;
using VF.Utils;

namespace VF.Utils {
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    internal class VFInitAttribute : Attribute {
    }
}

namespace VF.Hooks {
    internal static class VFInitHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            VfInitTimingLoggerMenuItem.Run(
                () => RuntimeHelpers.RunClassConstructor(typeof(HarmonyTest).TypeHandle),
                "Harmony warmup"
            );

            foreach (var method in TypeCache.GetMethodsWithAttribute<VFInitAttribute>()
                         .OrderBy(m => m.DeclaringType?.FullName)
                         .ThenBy(m => m.Name)) {
                if (!IsValid(method)) {
                    Debug.LogWarning($"[VRCFury] Invalid [VFInit] method {method.DeclaringType?.FullName}.{method.Name}");
                    continue;
                }

                try {
                    VfInitTimingLoggerMenuItem.Run(
                        () => method.Invoke(null, null),
                        $"{method.DeclaringType?.FullName}.{method.Name}"
                    );
                } catch (Exception e) {
                    Debug.LogException(new Exception(
                        $"Failed VFInit {method.DeclaringType?.FullName}.{method.Name}",
                        e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e
                    ));
                }
            }
        }

        private static bool IsValid(MethodInfo method) {
            return method.IsStatic
                   && method.ReturnType == typeof(void)
                   && method.GetParameters().Length == 0;
        }
    }
}
