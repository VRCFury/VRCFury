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
            var logTimings = EditorPrefs.GetBool("com.vrcfury.logVfInitTimings", false);
            var timings = logTimings ? new List<(string name, long ticks)>() : null;

            foreach (var method in TypeCache.GetMethodsWithAttribute<VFInitAttribute>()
                         .OrderBy(m => m.DeclaringType?.FullName)
                         .ThenBy(m => m.Name)) {
                if (!IsValid(method)) {
                    Debug.LogWarning($"[VRCFury] Invalid [VFInit] method {method.DeclaringType?.FullName}.{method.Name}");
                    continue;
                }

                var sw = logTimings ? Stopwatch.StartNew() : null;
                try {
                    method.Invoke(null, null);
                } catch (Exception e) {
                    Debug.LogException(new Exception(
                        $"Failed VFInit {method.DeclaringType?.FullName}.{method.Name}",
                        e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e
                    ));
                } finally {
                    if (sw != null) {
                        sw.Stop();
                        timings.Add(($"{method.DeclaringType?.FullName}.{method.Name}", sw.ElapsedTicks));
                    }
                }
            }

            if (!logTimings) return;
            Debug.Log(
                "[VRCFury] VFInit timings:\n" +
                string.Join("\n", timings
                    .OrderByDescending(x => x.ticks)
                    .Select(x => $"{TimeSpan.FromTicks(x.ticks).TotalMilliseconds:F1} ms  {x.name}"))
            );
        }

        private static bool IsValid(MethodInfo method) {
            return method.IsStatic
                   && method.ReturnType == typeof(void)
                   && method.GetParameters().Length == 0;
        }
    }
}
