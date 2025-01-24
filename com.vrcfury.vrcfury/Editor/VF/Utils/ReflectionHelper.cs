using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal class ReflectionHelper {
        public static bool IsReady<T>() where T : ReflectionHelper {
            var type = typeof(T);
            foreach (var field in type.GetFields(BindingFlags.Static)) {
                if (field.GetValue(null) == null) return false;
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
                foreach (var field in helper.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    if (field.GetValue(null) == null) {
                        notReady.Add(helper.FullName + "." + field.Name);
                    }
                }
            }
            if (notReady.Any()) {
                Debug.LogError("VRCFury failed to find hook into some parts of Unity properly. Perhaps this version of Unity is not yet supported?\n" + notReady.Join('\n'));
            }
        }
    }
}
