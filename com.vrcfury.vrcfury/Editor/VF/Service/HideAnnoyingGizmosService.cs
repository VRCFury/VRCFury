using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Service {
    /**
     * Hides annoying gizmos for things added by vrcfury, unless their object is specifically selected in the hierarchy.
     */
    [VFService]
    internal class HideAnnoyingGizmosService {
        [VFAutowired] private readonly AvatarManager avatarManager;
        private readonly HashSet<UnityEngine.Component> existingComponents = new HashSet<UnityEngine.Component>();
        
        [FeatureBuilderAction(FeatureOrder.CollectExistingComponents)]
        public void CollectExistingComponents() {
            existingComponents.UnionWith(avatarManager.AvatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>());
        }
        
        [FeatureBuilderAction(FeatureOrder.HideAddedComponents)]
        public void HideAddedComponents() {
            Hide(avatarManager.AvatarObject, existingComponents);
        }

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
