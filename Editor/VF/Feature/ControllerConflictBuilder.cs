using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * This builder is responsible for scanning the generated controllers, and complaining if you've done something bad
     * (like using two separate locomotion controllers as inputs).
     * It also handles other controller merge conflict issues, like making VRCPlayableLayerControl only affect
     * the layers from the controller that triggered it.
     */
    public class ControllerConflictBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.ControllerConflictCheck)]
        public void Apply() {

            var singleOwnerTypes = new HashSet<VRCAvatarDescriptor.AnimLayerType>() {
                VRCAvatarDescriptor.AnimLayerType.Base,
                VRCAvatarDescriptor.AnimLayerType.TPose,
                VRCAvatarDescriptor.AnimLayerType.IKPose,
                VRCAvatarDescriptor.AnimLayerType.Sitting
            };
            
            foreach (var controller in manager.GetAllTouchedControllers()) {
                var type = controller.GetType();
                var typeName = VRCFEnumUtils.GetName(type);
                var uniqueOwners = controller.GetLayerOwners();
                if (uniqueOwners.Count > 1) {
                    if (singleOwnerTypes.Contains(type)) {
                        throw new VRCFBuilderException(
                            "Your avatar contains multiple implementations for a base playable layer." +
                            " Usually, this means you are trying to add GogoLoco, but your avatar already has a Base controller." +
                            " The fix is usually to remove the custom Base controller that came with your avatar on the VRC Avatar Descriptor.\n\n" +
                            "Layer type: " + VRCFEnumUtils.GetName(type) + "\n" +
                            "Sources:\n" + string.Join("\n", uniqueOwners)
                        );
                    }

                    foreach (var owner in uniqueOwners) {
                        var layers = controller.GetLayers().ToList();
                        var ownedLayers = layers
                            .Where(layer => controller.GetLayerOwner(layer) == owner).ToList();
                        var ownedLayerIds = ownedLayers.Select(layer => layers.FindIndex(l => l == layer)).ToList();

                        foreach (var layer in ownedLayers) {
                            AnimatorIterator.ForEachBehaviour(layer, (b, add) => {
                                if (b is VRCPlayableLayerControl playableControl && VRCFEnumUtils.GetName(playableControl.layer) == typeName) {
                                    foreach (var ownedLayerId in ownedLayerIds) {
                                        var layerControl = (VRCAnimatorLayerControl)add(typeof(VRCAnimatorLayerControl));
                                        layerControl.playable = VRCFEnumUtils.Parse<VRC_AnimatorLayerControl.BlendableLayer>(typeName);
                                        layerControl.layer = ownedLayerId;
                                        layerControl.goalWeight = playableControl.goalWeight;
                                        layerControl.blendDuration = playableControl.blendDuration;
                                        layerControl.debugString = playableControl.debugString;
                                    }
                                    //return false;
                                }
                                return true;
                            });

                            if (type == VRCAvatarDescriptor.AnimLayerType.Action) {
                                
                            }
                        }
                    }
                }
            }
        }
    }
}
