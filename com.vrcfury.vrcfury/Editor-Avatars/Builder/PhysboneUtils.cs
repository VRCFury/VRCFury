using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder {
    internal static class PhysboneUtils {
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

        // If the user has attached something "visible" to the object (like a mesh)
        // or has constrained something to the object (and the constrained object may contain a mesh)
        // then it means they probably didn't want to exclude this from physbones.
        private static bool ContainsBonesUsedExternally(VFGameObject obj) {
            foreach (var s in obj.root.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                foreach (var bone in s.bones.AsVf()) {
                    if (bone && bone.IsChildOf(obj)) return true;
                }

                var rootBone = s.rootBone.asVf();
                if (rootBone && rootBone.IsChildOf(obj)) return true;
            }

            var usedAsConstraintSource = obj.root.GetConstraints(includeChildren: true)
                .SelectMany(constraint => constraint.GetSources())
                .NotNull()
                .Any(source => source.IsChildOf(obj));
            if (usedAsConstraintSource) {
                return true;
            }
            if (obj.GetComponentsInSelfAndChildren<Renderer>().Length > 1) {
                return true;
            }
            return false;
        }
    }
}
