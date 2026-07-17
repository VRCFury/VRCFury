using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
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
            var timings = new List<(string name, long ticks)>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<VFInitAttribute>()
                         .OrderBy(m => m.DeclaringType?.FullName)
                         .ThenBy(m => m.Name)) {
                if (!IsValid(method)) {
                    Debug.LogWarning($"[VRCFury] Invalid [VFInit] method {method.DeclaringType?.FullName}.{method.Name}");
                    continue;
                }

                var sw = Stopwatch.StartNew();
                try {
                    method.Invoke(null, null);
                } catch (Exception e) {
                    Debug.LogException(new Exception(
                        $"Failed VFInit {method.DeclaringType?.FullName}.{method.Name}",
                        e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e
                    ));
                } finally {
                    sw.Stop();
                    timings.Add(($"{method.DeclaringType?.Name}.{method.Name}", sw.ElapsedTicks));
                }
            }

            // Debug.Log(
            //     "[VRCFury] InitializeOnLoad timings:\n" +
            //     string.Join("\n", timings
            //         .OrderByDescending(x => x.ticks)
            //         .Select(x => $"{TimeSpan.FromTicks(x.ticks).TotalMilliseconds:F1} ms  {x.name}"))
            // );
        }

        private static bool IsValid(MethodInfo method) {
            return method.IsStatic
                   && method.ReturnType == typeof(void)
                   && method.GetParameters().Length == 0;
        }
    }
}
