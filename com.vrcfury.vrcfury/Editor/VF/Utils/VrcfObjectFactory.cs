using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class VrcfObjectFactory {
        private static readonly List<Object> created = new List<Object>();

        [InitializeOnLoadMethod]
        private static void OnLoad() {
            Scheduler.Schedule(Prune, 0);
        }

        private static void Prune() {
            var removed = 0;
            foreach (var asset in created) {
                if (asset == null) continue;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) continue;
                Object.DestroyImmediate(asset);
                removed++;
            }
            created.Clear();

            if (removed > 0) {
                //Debug.Log($"VRCF pruned {removed} unused assets");
            }
        }

        public static T Create<T>() where T : Object {
            return Create(typeof(T)) as T;
        }
        public static Object Create(Type type) {
            Object obj;
            if (typeof(ScriptableObject).IsAssignableFrom(type)) {
                obj = ScriptableObject.CreateInstance(type) as Object;
            } else {
                obj = Activator.CreateInstance(type) as Object;
            }
            if (obj == null) {
                throw new Exception("Failed to create instance of Object " + type.FullName);
            }

            Register(obj);
            return obj;
        }

        public static T Register<T>(T obj) where T : Object {
            created.Add(obj);
            return obj;
        }
    }
}
