using VF.Feature.Base;
using VF.Injector;
using VF.Service;

namespace VF.Feature {
    public class DriveParameterBuilder : FeatureBuilder {
        [VFAutowired] private readonly DriveParameterService paramService;
        
        [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
        public void Apply() {
            paramService.ApplyTriggers();
        }
    }
}
