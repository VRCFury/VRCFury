using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;

namespace VF.Utils {
    internal static class AnimationBindingUtils {
        internal static VFGameObject ResolveTarget(
            VFGameObject ownerObject,
            VFGameObject animatorObject,
            string path,
            Type type,
            bool rootBindingsApplyToAvatar = false,
            Func<VFGameObject, string, VFGameObject> findObject = null
        ) {
            if (animatorObject == null) return null;
            if (ownerObject == null) return null;
            if (path == null) return null;
            if (type == typeof(Animator)) return animatorObject;
            if (path == "" && rootBindingsApplyToAvatar) {
                return animatorObject;
            }
            VFGameObject Find(VFGameObject from, string relativePath) {
                return findObject != null ? findObject(from, relativePath) : from.Find(relativePath);
            }
            VFGameObject Parent(VFGameObject obj) {
                return findObject != null ? Find(obj, "..") : obj.parent;
            }

            var ancestor = ownerObject;
            while (ancestor != null && ancestor != animatorObject) {
                ancestor = Parent(ancestor);
            }
            if (ancestor != animatorObject) return null;

            if (ownerObject == animatorObject) {
                var target = Find(animatorObject, path);
                return IsValidResolvedTarget(target, type) ? target : null;
            }

            VFGameObject current = ownerObject;
            while (current != null) {
                var target = Find(current, path);
                if (IsValidResolvedTarget(target, type)) {
                    return target;
                }

                if (current == animatorObject) break;
                current = Parent(current);
            }
            return null;
        }

        private static bool IsValidResolvedTarget(VFGameObject target, Type type) {
            if (target == null) return false;
            if (type == typeof(GameObject)) return true;
            if (!typeof(UnityEngine.Component).IsAssignableFrom(type)) return false;
            if (target.GetComponent(type) != null) return true;

            if (type == typeof(BoxCollider)
                && target.GetComponents().Any(component => component.GetType().Name == "VRCStation")) return true;
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (typeof(IConstraint).IsAssignableFrom(type)
                && target.GetComponents<VRCConstraintBase>().Any()) return true;
            if (typeof(VRCConstraintBase).IsAssignableFrom(type)
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
