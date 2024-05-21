using System.Linq;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;

namespace VF.Service {
    [VFService]
    public class ArmatureLinkService {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FindAnimatedTransformsService findAnimatedTransformsService;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.ArmatureLink)]
        public void Apply() {
            ArmatureLinkBuilder.ApplyAll(
                findAnimatedTransformsService,
                mover,
                globals.avatarObject,
                globals.allFeaturesInRun.OfType<ArmatureLink>().ToList(),
                manager
            );
        }
    }
}
