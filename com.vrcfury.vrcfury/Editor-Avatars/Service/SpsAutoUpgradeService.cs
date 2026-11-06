using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class SpsAutoUpgradeService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly SpsUpgrader spsUpgrader;
        
        [FeatureBuilderAction(FeatureOrder.AutoUpgradeSps)]
        public void Apply() => Apply(AutoUpgradeDpsMenuItem.Get());

        public void Apply(bool autoUpgrade) {
            if (autoUpgrade) {
                spsUpgrader.Apply(avatarObject, false);
                return;
            }

            // Existing SPS components still need their legacy inputs captured before baking.
            SpsPlugPreparation.ImportSettingsFromLegacySystems(avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>());
            SpsSocketPreparation.ImportSettingsFromLegacySystems(avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>(), deleteLights: true);
        }
    }
}
