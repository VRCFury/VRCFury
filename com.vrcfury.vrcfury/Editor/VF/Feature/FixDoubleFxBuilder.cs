using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixDoubleFxBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixDoubleFx)]
        public void Apply() {
            var avatars = avatarObject.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>();
            foreach (var avatar in avatars) {
                var fxLayers = new List<RuntimeAnimatorController>();
                var fxLayerIds = new List<int>();
                var fxLayerDefault = new List<bool>();
                for (var i = 0; i < avatar.baseAnimationLayers.Length; i++) {
                    var layer = avatar.baseAnimationLayers[i];
                    if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX) {
                        fxLayers.Add(layer.isDefault ? null : layer.animatorController);
                        fxLayerIds.Add(i);
                        fxLayerDefault.Add(layer.isDefault);
                    }
                }

                if (fxLayers.Count > 1) {
                    var uniqueFx = fxLayers
                        .Where(l => l != null)
                        .Distinct()
                        .ToList();
                    if (uniqueFx.Count > 1) {
                        throw new VRCFBuilderException(
                            "Avatar contains more than one unique FX playable layer." +
                            " Check the Avatar Descriptor, and remove the FX controller that shouldn't be there.");
                    }
                    var allDefault = fxLayerDefault.All(a => a);
                    foreach (var id in fxLayerIds) {
                        var layer = avatar.baseAnimationLayers[id];
                        if (id == fxLayerIds[0]) {
                            layer.type = VRCAvatarDescriptor.AnimLayerType.Action;
                            layer.isDefault = true;
                        } else if (allDefault) {
                            layer.isDefault = true;
                            layer.animatorController = null;
                        } else if (id == fxLayerIds[1]) {
                            layer.isDefault = false;
                            layer.animatorController = uniqueFx.Count > 0 ? uniqueFx[0] : null;
                        } else {
                            layer.isDefault = false;
                            layer.animatorController = null;
                        }
                        avatar.baseAnimationLayers[id] = layer;
                    }
                }
            }
        }
    }
}
