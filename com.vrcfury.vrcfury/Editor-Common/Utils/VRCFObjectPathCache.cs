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
        [VFAutowired] [CanBeNull] private VFGameObject avatarObject;
        private readonly List<Snapshot> snapshots = new List<Snapshot>();

        private class Snapshot {
            public readonly Dictionary<string, VFGameObject> pathToObject = new Dictionary<string, VFGameObject>();
            public readonly Dictionary<VFGameObject, string> objectToPath = new Dictionary<VFGameObject, string>();
            public readonly Dictionary<VFGameObject, VFGameObject> objectToParent = new Dictionary<VFGameObject, VFGameObject>();
        }

        public void Capture() {
            if (avatarObject == null) return;
            var snapshot = new Snapshot();
            foreach (var obj in avatarObject.GetSelfAndAllChildren()) {
                var path = obj.GetPath(avatarObject);
                if (!snapshot.pathToObject.ContainsKey(path)) {
                    snapshot.pathToObject[path] = obj;
                }
                snapshot.objectToPath[obj] = path;
                snapshot.objectToParent[obj] = obj == avatarObject ? null : obj.parent;
            }
            snapshots.Add(snapshot);
        }

        public static VRCFObjectPathCache GetPerFrame(VFGameObject baseObject) {
            return perFrame.GetOrCreate(baseObject, () => {
                var cache = new VRCFObjectPathCache {
                    avatarObject = baseObject
                };
                cache.Capture();
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
            if (snapshots.Count == 0) {
                var root = avatarObject ?? obj?.root;
                if (obj == null || root == null || !obj.IsSameOrChildOf(root)) return null;
                return obj == root ? null : obj.parent;
            }
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
            if (snapshots.Count == 0) {
                var root = avatarObject ?? from.root;
                if (!from.IsSameOrChildOf(root)) return null;
                var current = relativePath.StartsWith("/") ? root : from;
                foreach (var part in relativePath.Split('/')) {
                    if (part == "" || part == ".") continue;
                    if (part == "..") {
                        if (current == root) return null;
                        current = current.parent;
                    } else {
                        current = current.Find(part);
                    }
                    if (current == null) return null;
                }
                return current;
            }
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
