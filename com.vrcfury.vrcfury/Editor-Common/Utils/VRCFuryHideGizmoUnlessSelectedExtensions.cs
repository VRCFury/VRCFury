using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Utils {
    internal static class VRCFuryHideGizmoUnlessSelectedExtensions {
        public static void Hide(VFGameObject root, ISet<UnityEngine.Component> ignore = null) {
            if (!Application.isPlaying) return;
            foreach (var c in root.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                if (ignore != null && ignore.Contains(c)) continue;
                if (c.owner().GetComponent<VRCFuryHideGizmoUnlessSelected>() != null) continue;
                if (c is ContactBase || c is Light || c is VRCPhysBone || c is VRCPhysBoneCollider) {
                    c.owner().AddComponent<VRCFuryHideGizmoUnlessSelected>();
                }
            }
        }
    }
}
