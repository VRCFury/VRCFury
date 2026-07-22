using System.Collections.Generic;
using JetBrains.Annotations;
using VF.Utils;

namespace VF.Builder {
    internal static class VRCFObjectPathCache {
        private static readonly Dictionary<string, VFGameObject> pathToObject = new Dictionary<string, VFGameObject>();
        private static readonly Dictionary<VFGameObject, string> objectToPath = new Dictionary<VFGameObject, string>();
        private static readonly Dictionary<VFGameObject, VFGameObject> objectToParent = new Dictionary<VFGameObject, VFGameObject>();

        public static void ClearCache() {
            pathToObject.Clear();
            objectToPath.Clear();
            objectToParent.Clear();
        }

        public static void WarmupCache(VFGameObject baseObject) {
            foreach (var obj in baseObject.GetSelfAndAllChildren()) {
                var path = obj.GetPath();
                pathToObject[path] = obj;
                objectToPath[obj] = path;
                objectToParent[obj] = obj.parent;
            }
        }

        [CanBeNull]
        public static VFGameObject GetParent(VFGameObject obj) {
            return objectToParent.TryGetValue(obj, out var parent) ? parent : obj.parent;
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(ClearCache, 0);
        }

        [CanBeNull]
        public static VFGameObject Find(VFGameObject from, string relativePath) {
            if (relativePath == "") return from;
            if (objectToPath.TryGetValue(from, out var fromPath)) {
                var toPath = AnimationBindingUtils.JoinPaths(fromPath, relativePath);
                return pathToObject.TryGetValue(toPath, out var to) ? to : null;
            }
            return from.Find(relativePath);
        }
    }
}
