using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Service;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    public class SpsSendersForAllBuilder {
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction(FeatureOrder.GiveEverythingSpsSenders)]
        public void Apply() {
            var hasSps = globals.avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any()
                         || globals.avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any();
            var allowAuto = AutoUpgradeDpsMenuItem.Get();

            if (!hasSps && !allowAuto) {
                return;
            }

            RemoveTPSSenders();
            SpsUpgrader.Apply(globals.avatarObject, false, SpsUpgrader.Mode.AutomatedForEveryone);
        }

        private void RemoveTPSSenders() {
            foreach (var sender in globals.avatarObject.GetComponentsInSelfAndChildren<VRCContactSender>()) {
                if (IsTPSSender(sender)) {
                    Debug.Log("Deleting TPS sender on " + sender.owner().GetPath());
                    AvatarCleaner.RemoveComponent(sender);
                }
            }
        }

        private static bool IsTPSSender(VRCContactSender c) {
            if (c.collisionTags.Any(t => t == HapticUtils.CONTACT_PEN_MAIN)) return true;
            if (c.collisionTags.Any(t => t == HapticUtils.CONTACT_PEN_WIDTH)) return true;
            if (c.collisionTags.Any(t => t == HapticUtils.TagTpsOrfRoot)) return true;
            if (c.collisionTags.Any(t => t == HapticUtils.TagTpsOrfFront)) return true;
            return false;
        }
    }
}
