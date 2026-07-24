using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using VF.Injector;
using VF.Utils;

namespace VF.Builder {
    [VFService]
    internal class VRCFObjectPathCache {
        private static readonly Dictionary<VFGameObject, VRCFObjectPathCache> perFrame
            = new Dictionary<VFGameObject, VRCFObjectPathCache>();
        private readonly List<Snapshot> snapshots = new List<Snapshot>();

        private class Snapshot {
            public readonly Dictionary<string, VFGameObject> pathToObject = new Dictionary<string, VFGameObject>();
            public readonly Dictionary<VFGameObject, string> objectToPath = new Dictionary<VFGameObject, string>();
            public readonly Dictionary<VFGameObject, VFGameObject> objectToParent = new Dictionary<VFGameObject, VFGameObject>();
        }

        public void Capture(VFGameObject baseObject) {
            var snapshot = new Snapshot();
            foreach (var obj in baseObject.GetSelfAndAllChildren()) {
                var path = obj.GetPath(baseObject);
                if (!snapshot.pathToObject.ContainsKey(path)) {
                    snapshot.pathToObject[path] = obj;
                }
                snapshot.objectToPath[obj] = path;
                snapshot.objectToParent[obj] = obj == baseObject ? null : obj.parent;
            }
            snapshots.Add(snapshot);
        }

        public static VRCFObjectPathCache GetPerFrame(VFGameObject baseObject) {
            return perFrame.GetOrCreate(baseObject, () => {
                var cache = new VRCFObjectPathCache();
                cache.Capture(baseObject);
                return cache;
            });
        }

        [VFInit]
        private static void Init() {
            Scheduler.Schedule(perFrame.Clear, 0);
        }

        private IEnumerable<Snapshot> GetSnapshots(bool reverse = false) {
            return reverse ? snapshots.AsEnumerable().Reverse() : snapshots;
        }

        [CanBeNull]
        public VFGameObject GetParent(VFGameObject obj, bool reverse = false) {
            foreach (var snapshot in GetSnapshots(reverse)) {
                if (!snapshot.objectToParent.TryGetValue(obj, out var parent)) continue;
                if (ReferenceEquals(parent, null)) return null;
                if (parent != null) return parent;
            }
            return null;
        }

        [CanBeNull]
        public VFGameObject Find(VFGameObject from, string relativePath, bool reverse = false) {
            if (from == null || relativePath == null) return null;
            if (relativePath == "") return from;
            foreach (var snapshot in GetSnapshots(reverse)) {
                if (!snapshot.objectToPath.TryGetValue(from, out var fromPath)) continue;
                var toPath = AnimationBindingUtils.ResolveRelativePath(fromPath, relativePath);
                if (toPath == null) return null;
                if (snapshot.pathToObject.TryGetValue(toPath, out var to) && to != null) return to;
            }
            return null;
        }
    }
}
