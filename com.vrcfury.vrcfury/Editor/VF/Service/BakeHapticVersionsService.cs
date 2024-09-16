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
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        private ControllerManager fx => controllers.GetFx();
        
        // Bump when plug senders or receivers are changed
        private const int LocalVersion = 10;
        
        // Bump when any senders are changed
        private const int BeaconVersion = 7;

        [FeatureBuilderAction]
        public void Apply() {
            if (!avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any()
                && !avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any()) {
                return;
            }

            // Add a version beacon so nearby clients know what compatibility level to use
            var parent = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Chest)
                         ?? VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Hips)
                         ?? avatarObject;
            var beaconRoot = GameObjects.Create("vfh_versionbeacon", parent);
            var versionBeaconTag = "VFH_VERSION_" + BeaconVersion;
            hapticContacts.AddSender(new HapticContactsService.SenderRequest() {
                obj = beaconRoot,
                objName = "VersionBeacon",
                radius = 0.01f,
                tags = new [] { versionBeaconTag }
            });
            
            var hasOgbReceivers = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any(c => !c.sendersOnly)
                                  || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any(c => !c.sendersOnly);
            hasOgbReceivers &= HapticsToggleMenuItem.Get();

            if (hasOgbReceivers) {
                // Add a parameter to FX so it can be picked up by client apps
                // Because of https://feedback.vrchat.com/bug-reports/p/oscquery-provides-wrong-values-for-avatar-parameters-until-they-are-changed
                // we can't just use an int and set it to the version number.
                fx.NewBool($"VFH/Version/{LocalVersion}", usePrefix: false, synced: true, networkSynced: false);
                
                var receiveTags = new List<string>() { versionBeaconTag };
                if (BeaconVersion == 7) receiveTags.Add("OGB_VERSION_6");
                hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                    obj = beaconRoot,
                    paramName = "VFH/Beacon",
                    objName = "BeaconReceiver",
                    radius = 3f, // this is the max radius that vrc will allow
                    tags = receiveTags.ToArray(),
                    party = HapticUtils.ReceiverParty.Others,
                    usePrefix = false,
                    localOnly = true
                });
            }
        }
    }
}
