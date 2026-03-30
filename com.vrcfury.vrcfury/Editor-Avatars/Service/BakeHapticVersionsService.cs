using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;

namespace VF.Service {
    /** Adds a parameter to the avatar so OGB can pick up what version of haptics are available */
    [VFService]
    internal class BakeHapticVersionsService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        private ControllerManager fx => controllers.GetFx();
        
        // Bump when plug senders or receivers are changed
        private const int LocalVersion = 10;

        [FeatureBuilderAction]
        public void Apply() {
            var hasOgbReceivers = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any(c => !c.sendersOnly)
                                  || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any(c => !c.sendersOnly);
            hasOgbReceivers &= HapticsToggleMenuItem.Get();

            if (hasOgbReceivers) {
                // Add a parameter to FX so it can be picked up by client apps
                // Because of https://feedback.vrchat.com/bug-reports/p/oscquery-provides-wrong-values-for-avatar-parameters-until-they-are-changed
                // we can't just use an int and set it to the version number.
                fx.NewBool($"VFH/Version/{LocalVersion}", usePrefix: false, synced: true, networkSynced: false);
            }
        }
    }
}
