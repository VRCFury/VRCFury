using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Service {
    /**
     * A lot of VRCFury's internal classes assume they can find animatable paths to objects
     * by finding the nearest VRCAvatarDescriptor. If an avatar contains multiple VRCAvatarDescriptors,
     * these paths can be wrong. Since the inner VRCAvatarDescriptors don't do anything anyways, we can just
     * prune them to ensure the paths will use the correct one.
     */
    [VFService]
    internal class RemoveExtraDescriptorsService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction(FeatureOrder.RemoveExtraDescriptors)]
        public void Apply() {
            foreach (var descriptor in avatarObject.GetComponentsInSelfAndChildren<VRCAvatarDescriptor>()) {
                if (descriptor.owner() != avatarObject) {
                    Object.DestroyImmediate(descriptor);
                }
            }
        }
    }
}
