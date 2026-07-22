using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDKBase.Validation.Performance;
#endif

namespace VF.Utils {
    internal static class AnimationBindingUtils {
        internal static VFGameObject ResolveTarget(
            VFGameObject ownerObject,
            VFGameObject animatorObject,
            string path,
            Type type,
            bool rootBindingsApplyToAvatar = false,
            bool useCachedPaths = true
        ) {
            if (animatorObject == null) return null;
            if (ownerObject == null) return null;
            if (path == null) return null;
            if (path == "" && rootBindingsApplyToAvatar) {
                return animatorObject;
            }
            var ancestor = ownerObject;
            while (ancestor != null && ancestor != animatorObject) {
                ancestor = useCachedPaths ? VRCFObjectPathCache.GetParent(ancestor) : ancestor.parent;
            }
            if (ancestor != animatorObject) return null;

            VFGameObject current = ownerObject;
            while (current != null) {
                var target = useCachedPaths
                    ? VRCFObjectPathCache.Find(current, path)
                    : current.Find(path);
                if (IsValidResolvedTarget(target, type)) {
                    return target;
                }

                if (current == animatorObject) break;
                current = useCachedPaths ? VRCFObjectPathCache.GetParent(current) : current.parent;
            }
            return null;
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

        internal static string JoinPaths(string a, string b, bool allowAdvancedOperators = true) {
            var output = new List<string>();
            foreach (var path in new[] { a, b }) {
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith("/") && allowAdvancedOperators) output.Clear();
                foreach (var part in path.Split('/')) {
                    if (part == ".." && output.Count > 0 && output[output.Count - 1] != ".." && allowAdvancedOperators) {
                        output.RemoveAt(output.Count - 1);
                    } else if (part == "." && allowAdvancedOperators) {
                    } else if (part != "") {
                        output.Add(part);
                    }
                }
            }
            return string.Join("/", output);
        }
    }
}
