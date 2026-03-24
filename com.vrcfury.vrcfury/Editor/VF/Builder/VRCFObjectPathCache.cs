using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using VF.Service;
using VF.Utils;

namespace VF.Builder {
    internal static class VRCFObjectPathCache {
        private static readonly Dictionary<string, VFGameObject> pathToObject = new Dictionary<string, VFGameObject>();
        private static readonly Dictionary<VFGameObject, string> objectToPath = new Dictionary<VFGameObject, string>();

        public static void ClearCache() {
            pathToObject.Clear();
            objectToPath.Clear();
        }

        public static void WarmupCache(VFGameObject baseObject) {
            foreach (var obj in baseObject.GetSelfAndAllChildren()) {
                var path = obj.GetPath();
                pathToObject[path] = obj;
                objectToPath[obj] = path;
            }
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            Scheduler.Schedule(ClearCache, 0);
        }

        [CanBeNull]
        public static VFGameObject Find(VFGameObject from, string relativePath) {
            if (objectToPath.TryGetValue(from, out var fromPath)) {
                var toPath = ClipRewritersService.Join(fromPath, relativePath);
                return pathToObject.TryGetValue(toPath, out var to) ? to : null;
            }
            return from.Find(relativePath);
        }
    }
}
