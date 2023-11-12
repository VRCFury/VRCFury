using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder {
    public class PhysboneUtils {
        public static IList<Transform> GetAffectedTransforms(VRCPhysBoneBase physBone) {
            bool IsIgnored(Transform transform) =>
                physBone.ignoreTransforms.Any(ignored => ignored != null && transform.IsChildOf(ignored));

            return physBone.GetRootTransform().asVf()
                .GetSelfAndAllChildren()
                .Select(o => o.transform)
                .Where(t => !IsIgnored(t))
                .ToArray();
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
