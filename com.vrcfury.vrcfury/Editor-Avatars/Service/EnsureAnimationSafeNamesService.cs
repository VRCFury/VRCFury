using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class EnsureAnimationSafeNamesService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction(FeatureOrder.EnsureAnimationSafeNames)]
        public void Apply() {
            foreach (var obj in avatarObject.GetSelfAndAllChildren()) {
                if (obj == avatarObject) continue;
                obj.EnsureAnimationSafeName();
            }
        }
    }
}
