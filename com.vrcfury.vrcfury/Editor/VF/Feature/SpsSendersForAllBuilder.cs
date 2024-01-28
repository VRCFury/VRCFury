using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;

namespace VF.Feature {
    [VFService]
    public class SpsSendersForAllBuilder {
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction(FeatureOrder.GiveEverythingSpsSenders)]
        public void Apply() {
            SpsUpgrader.Apply(globals.avatarObject, false, SpsUpgrader.Mode.AutomatedForEveryone);
        }
    }
}
