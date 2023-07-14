using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder {
    public class PhysboneUtils {
        public static void RemoveFromPhysbones(Transform obj, bool evenIfRendered = false) {
            if (!evenIfRendered && ContainsRenderer(obj)) {
                return;
            }
            foreach (var physbone in obj.root.GetComponentsInChildren<VRCPhysBone>(true)) {
                var root = physbone.GetRootTransform();
                if (obj != root && obj.IsChildOf(root)) {
                    var alreadyExcluded = physbone.ignoreTransforms.Any(other => obj.IsChildOf(other));
                    if (!alreadyExcluded) {
                        physbone.ignoreTransforms.Add(obj);
                    }
                }
            }
        }

        private static bool ContainsRenderer(Transform obj) {
            foreach (var s in obj.root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                foreach (var bone in s.bones) {
                    if (bone && bone.IsChildOf(obj)) return true;
                }
                if (s.rootBone && s.rootBone.IsChildOf(obj)) return true;
            }
            foreach (var c in obj.root.GetComponentsInChildren<IConstraint>(true)) {
                for (var i = 0; i < c.sourceCount; i++) {
                    var t = c.GetSource(i).sourceTransform;
                    if (t && t.IsChildOf(obj)) return true;
                }
            }
            if (obj.GetComponentsInChildren<Renderer>(true).Length > 1) {
                return true;
            }
            return false;
        }
    }
}
