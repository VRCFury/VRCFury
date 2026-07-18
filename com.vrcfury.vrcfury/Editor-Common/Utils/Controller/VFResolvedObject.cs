using System;
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
            var unresolvedPath = sourcePath;
            if (unresolvedPath != null && context?.RewritePath != null) {
                unresolvedPath = context.RewritePath(unresolvedPath);
            }

            if (unresolvedPath == null) {
                return null;
            }

            var target = AnimationBindingUtils.ResolveTarget(
                context?.OwnerObject,
                context?.AnimatorObject,
                unresolvedPath,
                type,
                context?.RootBindingsApplyToAvatar ?? false,
                context?.FindObject
            );
            return new VFResolvedObject(target, sourcePath, unresolvedPath, target != null);
        }

        public string GetPath(VFGameObject root, string resolvedError) {
            if (target == null) return unresolvedPath;
            if (root == null) throw new Exception(resolvedError);
            return target.GetPath(root);
        }

        public string GetDebugPath(VFGameObject root = null) {
            if (target != null) return root != null ? target.GetPath(root) : target.GetPath();
            return unresolvedPath;
        }

        public bool ShouldDropOnSave() {
            if (isResolved) return target == null; // Resolved object was deleted during the build
            if (!isResolved) return unresolvedPath == null; // Binding path was rewritten to null during the load-in
            return false;
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
