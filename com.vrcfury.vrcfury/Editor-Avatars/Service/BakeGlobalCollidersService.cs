using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class BakeGlobalCollidersService {
        [VFAutowired] private readonly AvatarColliderService avatarColliderService;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.GlobalColliders)]
        public void Apply() {
            foreach (var globalContact in avatarObject.GetComponentsInSelfAndChildren<VRCFuryGlobalCollider>()) {
                avatarColliderService.CustomizeCollider(
                    avatarColliderService.GetNextFinger(),
                    globalContact.GetTransform(),
                    globalContact.radius,
                    globalContact.height
                );
            }
        }
    }
}
