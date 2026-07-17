using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Service {
    /**
     * Hides annoying gizmos for things added by vrcfury, unless their object is specifically selected in the hierarchy.
     */
    [VFService]
    internal class HideAnnoyingGizmosService {
        [VFAutowired] private readonly VFGameObject avatarObject;

        private readonly HashSet<UnityEngine.Component> existingComponents = new HashSet<UnityEngine.Component>();
        
        [FeatureBuilderAction(FeatureOrder.CollectExistingComponents)]
        public void CollectExistingComponents() {
            existingComponents.UnionWith(avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>());
        }
        
        [FeatureBuilderAction(FeatureOrder.HideAddedComponents)]
        public void HideAddedComponents() {
            VRCFuryHideGizmoUnlessSelectedExtensions.Hide(avatarObject, existingComponents);
        }
    }
}
