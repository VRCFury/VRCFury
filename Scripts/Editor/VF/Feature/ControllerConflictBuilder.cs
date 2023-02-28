using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Builder.Exceptions;
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

            var ownersByController = new Dictionary<VRCAvatarDescriptor.AnimLayerType, ISet<string>>();
            foreach (var controller in manager.GetAllUsedControllers()) {
                var type = controller.GetType();
                var uniqueOwners = controller.GetLayerOwners();
                ownersByController[type] = uniqueOwners;
                
                if (uniqueOwners.Count > 1 && singleOwnerTypes.Contains(type)) {
                    throw new VRCFBuilderException(
                        "Your avatar contains multiple implementations for a base playable layer." +
                        " Usually, this means you are trying to add GogoLoco, but your avatar already has a Base controller." +
                        " The fix is usually to remove the custom Base controller that came with your avatar on the VRC Avatar Descriptor.\n\n" +
                        "Layer type: " + VRCFEnumUtils.GetName(type) + "\n" +
                        "Sources:\n" + string.Join("\n", uniqueOwners)
                    );
                }
            }

            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetLayers()) {
                    var layerOwner = controller.GetLayerOwner(layer);
                    AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                        if (b is VRCPlayableLayerControl playableControl) {
                            var drivesTypeName = VRCFEnumUtils.GetName(playableControl.layer);
                            var drivesType = VRCFEnumUtils.Parse<VRCAvatarDescriptor.AnimLayerType>(drivesTypeName);
                            if (!ownersByController.TryGetValue(drivesType, out var uniqueOwnersOnType)) {
                                // They're driving a controller that doesn't exist?
                                // uhh... keep it I guess
                                return true;
                            }
                            if (!uniqueOwnersOnType.Contains(layerOwner)) return false;
                            if (uniqueOwnersOnType.Count == 1) return true;

                            var drivesController = manager.GetController(drivesType);
                            var drivesControllerLayers = drivesController.GetLayers()
                                .ToList();
                            var drivesLayers = drivesControllerLayers
                                .Where(l => drivesController.GetLayerOwner(l) == layerOwner)
                                .ToList();
                            var drivesLayerIds = drivesLayers
                                .Select(l => drivesControllerLayers.FindIndex(ll => ll == l))
                                .ToList();
                            foreach (var drivesLayerId in drivesLayerIds) {
                                var existingLayerWeight = drivesController.GetWeight(drivesLayerId);
                                var layerControl = (VRCAnimatorLayerControl)add(typeof(VRCAnimatorLayerControl));
                                layerControl.playable =
                                    VRCFEnumUtils.Parse<VRC_AnimatorLayerControl.BlendableLayer>(drivesTypeName);
                                layerControl.layer = drivesLayerId;
                                layerControl.goalWeight = playableControl.goalWeight;
                                layerControl.blendDuration = playableControl.blendDuration;
                                layerControl.debugString = playableControl.debugString;
                            }
                            return false;
                        }

                        return true;
                    });
                }
            }
            
            if (ownersByController.ContainsKey(VRCAvatarDescriptor.AnimLayerType.Action)
                && ownersByController[VRCAvatarDescriptor.AnimLayerType.Action].Count > 1) {
                var action = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Action);
                var enableLayer = action.NewLayer("VRCF Force Enable");
                var enable = enableLayer.NewState("Enable");
                var enableControl = VRCFAnimatorUtils.AddStateMachineBehaviour<VRCPlayableLayerControl>(enable.GetRaw());
                enableControl.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                enableControl.goalWeight = 1;
                var i = 0;
                foreach (var layer in action.GetLayers()) {
                    var layerNum = i++;
                    if (layerNum != 0) action.SetWeight(layer, 0);
                }
            }
            
            // TODO: Deal with conflicts when multiple owners:
            // * turn on/off locomotion
            // * turn on/off tracking
            // * turn on/off pose space
            // * re-enable the defaults layer on action when action is used?
        }
    }
}
