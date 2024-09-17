using System.Collections.Generic;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * Throws an error if you have more than one locomotion implementation on your avatar, and rips out the default
     * implementation if you've merged one in using a vrcfury Full Controller.
     */
    [VFService]
    internal class LocomotionConflictResolverService {
        [VFAutowired] private readonly ControllersService controllers;
        
        [FeatureBuilderAction(FeatureOrder.LocomotionConflictResolver)]
        public void Apply() {

            var singleOwnerTypes = new HashSet<VRCAvatarDescriptor.AnimLayerType>() {
                VRCAvatarDescriptor.AnimLayerType.Base,
                VRCAvatarDescriptor.AnimLayerType.TPose,
                VRCAvatarDescriptor.AnimLayerType.IKPose,
                VRCAvatarDescriptor.AnimLayerType.Sitting
            };

            foreach (var controller in controllers.GetAllUsedControllers()) {
                var type = controller.GetType();
                if (!singleOwnerTypes.Contains(type)) continue;
                var uniqueOwners = new HashSet<string>();
                foreach (var layer in controller.GetLayers()) {
                    if (!controller.IsSourceAvatarDescriptor(layer)) {
                        uniqueOwners.Add(controller.GetLayerOwner(layer));
                    }
                }

                if (uniqueOwners.Count > 1) {
                    throw new VRCFBuilderException(
                        "Your avatar contains multiple locomotion implementations." +
                        " You can only use one of these:\n\n" +
                        "Layer type: " + VRCFEnumUtils.GetName(type) + "\n" +
                        "Sources:\n" + string.Join("\n", uniqueOwners)
                    );
                }

                if (uniqueOwners.Count > 0) {
                    foreach (var layer in controller.GetLayers()) {
                        if (controller.IsSourceAvatarDescriptor(layer)) {
                            layer.Remove();
                        }
                    }
                }
            }
        }
    }
}
