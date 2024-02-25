using VF.Feature.Base;
using VF.Injector;
using VF.Service;

namespace VF.Feature {
    [VFService]
    public class FixAmbiguousObjectNamesBuilder {
        [VFAutowired] private readonly GlobalsService globals;

        [FeatureBuilderAction(FeatureOrder.FixAmbiguousObjectNames)]
        public void Apply() {
            foreach (var obj in globals.avatarObject.GetSelfAndAllChildren()) {
                if (obj == globals.avatarObject) continue;
                obj.EnsureAnimationSafeName();
            }
        }
    }
}