using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class DefaultAdditiveLayerFixBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.RemoveDefaultedAdditiveLayer)]
        public void Apply() {
            var descriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();

            for (int i=0; i<descriptor.baseAnimationLayers.Length; i++) {
                var layer = descriptor.baseAnimationLayers[i];
                if (
                    layer.type == VRCAvatarDescriptor.AnimLayerType.Additive && 
                    layer.isDefault &&
                    layer.animatorController == null
                ) {
                    descriptor.baseAnimationLayers[i].isDefault = false;
                }
            }
        }
    }
}