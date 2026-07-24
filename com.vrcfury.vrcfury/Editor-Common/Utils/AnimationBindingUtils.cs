using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Model.Feature;
using VF.Utils.Controller;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDKBase.Validation.Performance;
#endif

namespace VF.Utils {
    internal static class AnimationBindingUtils {
        internal static VFGameObject ResolveTarget(
            VFLoadContext context,
            string path,
            Type type
        ) {
            var ownerObject = context.OwnerObject;
            var animatorObject = context.AnimatorObject;
            var objectPaths = context.ObjectPaths;
            var reverseObjectPaths = context.ReverseObjectPaths;
            if (animatorObject == null) return null;
            if (ownerObject == null) return null;
            if (path == null) return null;
            if (path == "" && context.RootBindingsApplyToAvatar) {
                return animatorObject;
            }
            if (path.StartsWith("/")) {
                var target = objectPaths.Find(animatorObject, path.TrimStart('/'), reverseObjectPaths);
                return IsValidResolvedTarget(target, type) ? target : null;
            }

            var ancestor = ownerObject;
            while (ancestor != null && ancestor != animatorObject) {
                ancestor = objectPaths.GetParent(ancestor, reverseObjectPaths);
            }
            if (ancestor != animatorObject) return null;

            VFGameObject current = ownerObject;
            while (current != null) {
                var target = objectPaths.Find(current, path, reverseObjectPaths);
                if (IsValidResolvedTarget(target, type)) {
                    return target;
                }

                if (current == animatorObject) break;
                current = objectPaths.GetParent(current, reverseObjectPaths);
            }
            return null;
        }

        internal static string RewriteRelativePath(
            string path,
            IReadOnlyList<FullController.BindingRewrite> rewriteBindings
        ) {
            string AppendRewrite(string to, string suffix) {
                if (suffix == null) return to;
                if (to == "" || suffix.StartsWith("/")) return suffix;
                if (suffix == "") return to;
                return to == "/" ? "/" + suffix : to + "/" + suffix;
            }

            foreach (var rewrite in rewriteBindings) {
                var from = rewrite.from ?? "";
                while (from.Length > 1 && from.EndsWith("/")) from = from.Substring(0, from.Length - 1);
                var to = rewrite.to ?? "";
                while (to.Length > 1 && to.EndsWith("/")) to = to.Substring(0, to.Length - 1);

                if (from == "") {
                    path = AppendRewrite(to, path);
                    if (rewrite.delete) return null;
                } else if (path.StartsWith(from + "/")) {
                    path = AppendRewrite(to, path.Substring(from.Length + 1));
                    if (rewrite.delete) return null;
                } else if (path == from) {
                    path = to;
                    if (rewrite.delete) return null;
                }
            }

            return path;
        }

        internal static bool IsValidResolvedTarget(VFGameObject target, Type type) {
            if (target == null) return false;
            if (type == null) return false;
            if (type == typeof(GameObject)) return true;
            if (type == typeof(Animator)) return true;
            if (!typeof(UnityEngine.Component).IsAssignableFrom(type)) return false;
            if (target.GetComponent(type) != null) return true;

            if (type == typeof(BoxCollider)
                && target.GetComponents().Any(component => component.GetType().Name == "VRCStation")) return true;
#if VRCSDK_HAS_VRCCONSTRAINTS
            // Half-upgraded assets can temporarily point at the other kind of constraint.
            if (typeof(IConstraint).IsAssignableFrom(type)
                && target.GetComponents<IVRCConstraint>().Any()) return true;
            if (typeof(IVRCConstraint).IsAssignableFrom(type)
                && target.GetComponents<IConstraint>().Any()) return true;
#endif
            return false;
        }

        internal static string ResolveRelativePath(string a, string b) {
            var output = new List<string>();
            foreach (var path in new[] { a, b }) {
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith("/")) {
                    output.Clear();
                }
                foreach (var part in path.Split('/')) {
                    if (part == "..") {
                        if (output.Count == 0) return null;
                        output.RemoveAt(output.Count - 1);
                    } else if (part == ".") {
                    } else if (part != "") {
                        output.Add(part);
                    }
                }
            }
            return string.Join("/", output);
        }
    }
}
