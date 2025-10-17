using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class VrcfObjectFactory {
        private static readonly HashSet<Object> created = new HashSet<Object>();
        private static readonly HashSet<Object> doNotReuse = new HashSet<Object>();

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
            doNotReuse.Clear();

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

            if (obj is VRCExpressionParameters vp) {
                vp.parameters = new VRCExpressionParameters.Parameter[] { };
            }

            Register(obj);
            return obj;
        }

        public static T Register<T>(T obj) where T : Object {
            created.Add(obj);
            obj.hideFlags = HideFlags.DontSaveInEditor;
            return obj;
        }
        
        public static T DoNotReuse<T>(T obj) where T : Object {
            doNotReuse.Add(obj);
            return obj;
        }

        public static bool DidCreate(Object obj) {
            return created.Contains(obj);
        }
        
        public static bool IsMarkedAsDoNotReuse(Object obj) {
            return doNotReuse.Contains(obj);
        }
    }
}
