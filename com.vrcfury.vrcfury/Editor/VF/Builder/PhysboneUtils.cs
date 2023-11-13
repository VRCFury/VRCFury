using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder {
    public class PhysboneUtils {
        public class AffectedTransforms {
            public readonly List<Transform> mayRotate = new List<Transform>();
            public readonly List<Transform> mayMove = new List<Transform>();
        }
        public static AffectedTransforms GetAffectedTransforms(VRCPhysBoneBase physBone) {
            var output = new AffectedTransforms();

            bool IsIgnored(Transform transform) =>
                physBone.ignoreTransforms.Any(ignored => ignored != null && transform.IsChildOf(ignored));

            var stack = new Stack<VFGameObject>();
            stack.Push(physBone.GetRootTransform());
            while (stack.Count > 0) {
                var t = stack.Pop();
                if (IsIgnored(t)) continue;

                var nonIgnoredChildren = t.Children()
                    .Where(child => !IsIgnored(child))
                    .ToArray();

                if (nonIgnoredChildren.Length > 1 && physBone.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore) {
                    foreach (var c in nonIgnoredChildren) {
                        stack.Push(c);
                    }
                } else {
                    output.mayRotate.Add(t);
                    var allChildren = t.GetSelfAndAllChildren()
                        .Where(o => o != t)
                        .Select(o => o.transform)
                        .ToArray();
                    output.mayRotate.AddRange(allChildren);
                    output.mayMove.AddRange(allChildren);
                }
            }

            return output;
        }
        
        public static void RemoveFromPhysbones(VFGameObject obj, bool force = false) {
            if (!force && ContainsBonesUsedExternally(obj)) {
                return;
            }
            foreach (var physbone in obj.root.GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                var root = physbone.GetRootTransform();
                if (obj != root && obj.IsChildOf(root)) {
                    var alreadyExcluded = physbone.ignoreTransforms.Any(other => other != null && obj.IsChildOf(other));
                    if (!alreadyExcluded) {
                        physbone.ignoreTransforms.Add(obj);
                    }
                }
            }
        }

        private static bool ContainsBonesUsedExternally(VFGameObject obj) {
            foreach (var s in obj.root.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                foreach (var bone in s.bones) {
                    if (bone && bone.IsChildOf(obj)) return true;
                }
                if (s.rootBone && s.rootBone.IsChildOf(obj)) return true;
            }
            foreach (var c in obj.root.GetComponentsInSelfAndChildren<IConstraint>()) {
                for (var i = 0; i < c.sourceCount; i++) {
                    var t = c.GetSource(i).sourceTransform;
                    if (t && t.IsChildOf(obj)) return true;
                }
            }
            if (obj.GetComponentsInSelfAndChildren<Renderer>().Length > 1) {
                return true;
            }
            return false;
        }
    }
}
