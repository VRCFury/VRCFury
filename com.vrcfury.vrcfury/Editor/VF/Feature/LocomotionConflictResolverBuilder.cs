using System.Collections.Generic;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    /**
     * Throws an error if you have more than one locomotion implementation on your avatar, and rips out the default
     * implementation if you've merged one in using a vrcfury Full Controller.
     */
    [VFService]
    public class LocomotionConflictResolverBuilder {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.LocomotionConflictResolver)]
        public void Apply() {

            var singleOwnerTypes = new HashSet<VRCAvatarDescriptor.AnimLayerType>() {
                VRCAvatarDescriptor.AnimLayerType.Base,
                VRCAvatarDescriptor.AnimLayerType.TPose,
                VRCAvatarDescriptor.AnimLayerType.IKPose,
                VRCAvatarDescriptor.AnimLayerType.Sitting
            };

            foreach (var controller in manager.GetAllUsedControllers()) {
                var type = controller.GetType();
                if (!singleOwnerTypes.Contains(type)) continue;
                var uniqueOwners = new HashSet<string>();
                foreach (var layer in controller.GetLayers()) {
                    if (!controller.IsOwnerBaseAvatar(layer)) {
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
                        if (controller.IsOwnerBaseAvatar(layer)) {
                            controller.RemoveLayer(layer);
                        }
                    }
                }
            }
        }
    }
}
