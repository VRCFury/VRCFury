using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils {
    public static class VrcfObjectFactory {
        private static readonly List<Object> created = new List<Object>();

        [InitializeOnLoadMethod]
        public static void OnLoad() {
            EditorApplication.update += Prune;
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
            T obj;
            if (typeof(ScriptableObject).IsAssignableFrom(typeof(T))) {
                obj = ScriptableObject.CreateInstance(typeof(T)) as T;
            } else {
                obj = Activator.CreateInstance(typeof(T)) as T;
            }
            if (obj == null) {
                throw new Exception("Failed to create instance of " + typeof(T).FullName);
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
