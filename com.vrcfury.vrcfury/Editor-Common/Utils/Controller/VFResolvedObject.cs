using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VF.Utils.Controller;

namespace VF.Utils {
    internal readonly struct VFResolvedObject {
        public readonly VFGameObject target;
        private readonly string sourcePath;
        private readonly string unresolvedPath;
        private readonly bool isResolved;

        public VFResolvedObject(VFGameObject target, string sourcePath, string unresolvedPath, bool isResolved = false) {
            this.target = target;
            this.sourcePath = sourcePath;
            this.unresolvedPath = unresolvedPath;
            this.isResolved = isResolved;
        }

        public bool IsResolved => isResolved;
        public string SourcePath => sourcePath ?? "";
        public string UnresolvedPath => unresolvedPath;

        public static VFResolvedObject? Load(string sourcePath, VFLoadContext context, Type type) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var unresolvedPath = sourcePath;
            if (unresolvedPath != null && context.RewritePath != null) {
                unresolvedPath = context.RewritePath(unresolvedPath);
            }

            if (unresolvedPath == null) {
                return null;
            }

            var target = context.ResolveTarget(unresolvedPath, type);
            return new VFResolvedObject(target, sourcePath, unresolvedPath, target != null);
        }

        public string GetPath(
            [CanBeNull] VFGameObject root,
            Dictionary<VFGameObject, string> cachedPaths = null
        ) {
            if (target == null) return unresolvedPath;
            return GetTargetPath(root, cachedPaths) ?? unresolvedPath;
        }

        private string GetTargetPath(VFGameObject root, Dictionary<VFGameObject, string> cachedPaths) {
            if (cachedPaths != null && cachedPaths.TryGetValue(target, out var cachedPath)) {
                return cachedPath;
            }
            var path = root != null && !target.IsSameOrChildOf(root) ? null : target.GetPath(root);
            cachedPaths?.Add(target, path);
            return path;
        }

        public string GetPathForSave(VFGameObject root, Dictionary<VFGameObject, string> cachedPaths) {
            // A resolved binding is dropped if its target was deleted or moved outside this Animator.
            if (isResolved) return target == null ? null : GetTargetPath(root, cachedPaths);
            return unresolvedPath == null ? null : GetPath(root, cachedPaths);
        }

        public VFResolvedObject WithTarget(VFGameObject newTarget, bool newIsResolved) {
            return new VFResolvedObject(newTarget, sourcePath, unresolvedPath, newIsResolved);
        }

        public VFResolvedObject AsUnresolved(string newUnresolvedPath, bool newIsResolved = false) {
            return new VFResolvedObject(null, sourcePath, newUnresolvedPath, newIsResolved);
        }

        public override bool Equals(object obj) {
            if (!(obj is VFResolvedObject other)) return false;
            if (target != other.target) return false;
            if (target != null) return true;
            return unresolvedPath == other.unresolvedPath;
        }

        public override int GetHashCode() {
            return target != null
                ? HashCode.Combine(target)
                : HashCode.Combine(unresolvedPath);
        }
    }
}
