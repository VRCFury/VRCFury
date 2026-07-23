using System.Collections.Generic;
using JetBrains.Annotations;
using VF.Utils;

namespace VF.Builder {
    internal class VRCFObjectPathCache {
        private static readonly Dictionary<VFGameObject, VRCFObjectPathCache> perFrame
            = new Dictionary<VFGameObject, VRCFObjectPathCache>();
        private readonly Dictionary<string, VFGameObject> pathToObject = new Dictionary<string, VFGameObject>();
        private readonly Dictionary<VFGameObject, string> objectToPath = new Dictionary<VFGameObject, string>();
        private readonly Dictionary<VFGameObject, VFGameObject> objectToParent = new Dictionary<VFGameObject, VFGameObject>();

        public VRCFObjectPathCache(VFGameObject baseObject) {
            foreach (var obj in baseObject.GetSelfAndAllChildren()) {
                var path = obj.GetPath(baseObject);
                if (!pathToObject.ContainsKey(path)) {
                    pathToObject[path] = obj;
                }
                objectToPath[obj] = path;
                objectToParent[obj] = obj == baseObject ? null : obj.parent;
            }
        }

        public static VRCFObjectPathCache GetPerFrame(VFGameObject baseObject) {
            return perFrame.GetOrCreate(baseObject, () => new VRCFObjectPathCache(baseObject));
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(perFrame.Clear, 0);
        }

        [CanBeNull]
        public VFGameObject GetParent(VFGameObject obj) {
            return objectToParent.TryGetValue(obj, out var parent) ? parent : null;
        }

        [CanBeNull]
        public VFGameObject Find(VFGameObject from, string relativePath) {
            if (from == null || relativePath == null) return null;
            if (relativePath == "") return from;
            if (objectToPath.TryGetValue(from, out var fromPath)) {
                var toPath = AnimationBindingUtils.ResolveRelativePath(fromPath, relativePath);
                if (toPath == null) return null;
                return pathToObject.TryGetValue(toPath, out var to) ? to : null;
            }
            return null;
        }
    }
}
