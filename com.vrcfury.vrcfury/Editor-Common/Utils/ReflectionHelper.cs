using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    [AttributeUsage(AttributeTargets.Class)]
    internal class ReflectionHelperOptionalAttribute : Attribute {
    }

    internal class ReflectionHelper {
        private static IEnumerable<string> GetMissingDetails(string path, object value) {
            if (value is HarmonyUtils.PatchObj patch && patch.error != null) {
                yield return $"{path}: {patch.error}";
                yield break;
            }
            if (value == null) {
                yield return path;
                yield break;
            }
            if (value is ICollection collection) {
                if (collection.Count == 0) {
                    yield return $"{path}: Empty array";
                    yield break;
                }
                var i = 0;
                foreach (var child in collection) {
                    foreach (var detail in GetMissingDetails($"{path}[{i}]", child)) {
                        yield return detail;
                    }
                    i++;
                }
            }
        }

        public static bool IsReady<T>() where T : ReflectionHelper {
            var type = typeof(T);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
                if (GetMissingDetails(field.Name, field.GetValue(null)).Any()) return false;
            }
            return true;
        }

        internal static IReadOnlyList<string> GetMissingRequiredBindings() {
            var notReady = new List<string>();

            var helpers = TypeCache.GetTypesDerivedFrom<ReflectionHelper>();
            foreach (var helper in helpers) {
                if (helper.GetCustomAttribute<ReflectionHelperOptionalAttribute>() != null) continue;
                foreach (var field in helper.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
                    var value = field.GetValue(null);
                    notReady.AddRange(GetMissingDetails($"{helper.FullName}.{field.Name}", value));
                }
            }
            return notReady;
        }

        [VFInit]
        private static void Init() {
            var notReady = GetMissingRequiredBindings();
            if (notReady.Count > 0) {
                Debug.LogWarning("VRCFury failed to hook into some parts of Unity properly. Perhaps this version of Unity is not fully supported?\n" + notReady.Join('\n'));
            }
        }
    }
}
