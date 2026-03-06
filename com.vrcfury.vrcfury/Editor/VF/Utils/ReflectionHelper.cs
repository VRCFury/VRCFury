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
        private static bool IsMissing(object value) {
            if (value == null) return true;
            if (value is ICollection collection && collection.Count == 0) return true;
            return false;
        }

        public static bool IsReady<T>() where T : ReflectionHelper {
            var type = typeof(T);
            foreach (var field in type.GetFields(BindingFlags.Static)) {
                if (IsMissing(field.GetValue(null))) return false;
            }
            return true;
        }
        
        [InitializeOnLoadMethod]
        private static void Init() {
            var notReady = new List<string>();

            var helpers = typeof(ReflectionHelper).Assembly
                .GetTypes()
                .Where(cls => typeof(ReflectionHelper).IsAssignableFrom(cls))
                .ToArray();
            
            foreach (var helper in helpers) {
                //if (helper.GetCustomAttribute<ReflectionHelperOptionalAttribute>() != null) continue;
                foreach (var field in helper.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    if (IsMissing(field.GetValue(null))) {
                        notReady.Add(helper.FullName + "." + field.Name);
                    }
                }
            }
            if (notReady.Any()) {
                Debug.LogWarning("VRCFury failed to find hook into some parts of Unity properly. Perhaps this version of Unity is not fully supported?\n" + notReady.Join('\n'));
            }
        }
    }
}
