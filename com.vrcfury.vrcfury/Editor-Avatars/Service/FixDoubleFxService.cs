using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class FixDoubleFxService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;

        [FeatureBuilderAction(FeatureOrder.FixDoubleFx)]
        public void Apply() {
            avatar.FixInvalidLayers();
        }
    }
}
