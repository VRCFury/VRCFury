using VF.Builder;
using VF.Feature.Base;
using VF.Injector;

namespace VF.Service {
    [VFService]
    internal class ObjectCacheService {
        [VFAutowired] private readonly VRCFObjectPathCache objectPaths;
        [VFAutowired] private readonly VRCFArmatureCache armatureCache;

        [FeatureBuilderAction(FeatureOrder.CaptureInitialState)]
        public void CaptureInitialState() {
            objectPaths.Capture();
            armatureCache.Capture();
        }
    }
}
