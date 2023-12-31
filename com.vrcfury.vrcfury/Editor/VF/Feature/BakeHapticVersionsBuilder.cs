using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;

namespace VF.Builder.Haptics {
    /** Adds a parameter to the avatar so OGB can pick up what version of haptics are available */
    [VFService]
    public class BakeHapticVersionsBuilder : FeatureBuilder {
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        
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

            // Add a parameter to FX so it can be picked up by client apps
            // Because of https://feedback.vrchat.com/bug-reports/p/oscquery-provides-wrong-values-for-avatar-parameters-until-they-are-changed
            // we can't just use an int and set it to the version number.
            var fx = manager.GetFx();
            fx.NewBool($"VFH/Version/{LocalVersion}", usePrefix: false, synced: true, networkSynced: false);

            // Add a version beacon so nearby clients know what compatibility level to use
            var parent = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Chest)
                         ?? VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Hips)
                         ?? avatarObject;
            var beaconRoot = GameObjects.Create("vfh_versionbeacon", parent);
            var versionBeaconTag = "VFH_VERSION_" + BeaconVersion;
            hapticContacts.AddSender(beaconRoot, Vector3.zero, "VersionBeacon", 0.01f, new [] { versionBeaconTag });

            var receiveTags = new List<string>() { versionBeaconTag };
            if (BeaconVersion == 7) receiveTags.Add("OGB_VERSION_6");
            hapticContacts.AddReceiver(
                beaconRoot,
                Vector3.zero,
                "VFH/Beacon",
                "BeaconReceiver",
                3f, // this is the max radius that vrc will allow
                receiveTags.ToArray(),
                party: HapticUtils.ReceiverParty.Others,
                usePrefix: false,
                localOnly: true
            );
        }
    }
}
