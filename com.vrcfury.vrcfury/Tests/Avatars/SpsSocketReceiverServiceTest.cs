#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
namespace VF.Tests {
    using static VF.Inspector.VRCFuryHapticPlugEditor;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class SpsSocketReceiverServiceTest : SpsAvatarBuildTestBase {
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void LegacyAvailabilityControlsSocketRangeAndReceiverTags(bool available, bool defaultOn) {
            injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { legacyModeAvailable = available, legacyModeEnabledOnAvatarLoad = defaultOn }
            };
            root.transform.localScale = Vector3.one * 2;
            var factory = Socket();
            foreach (var party in new[] { Party.Self, Party.Others, Party.Merged }) {
                var outputs = factory(party);
                _ = outputs(SpsDepthMeters);
                _ = outputs(SpsPlugLengthMeters);
                _ = outputs(SpsPlugRadiusMeters);
            }
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            foreach (var receiver in contacts) {
                Assert.That(receiver.radius * receiver.transform.lossyScale.x,
                    Is.EqualTo(0.2f).Within(0.0001f));
                if (receiver.collisionTags.Contains(SpsTags.FormatPlug("test"))) {
                    Assert.That(receiver.collisionTags.Contains(SpsTags.LegacyPlugSphere),
                        Is.EqualTo(available && receiver.allowOthers));
                }
            }
            Assert.That(contacts.Any(r => r.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.EqualTo(available));
            Assert.That(fx.parameters.Any(p => p.name.Contains("Test/Others/Legacy")), Is.EqualTo(available));
        }
    }
}
#endif

namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;
    using Flags = VF.Component.VRCFuryHapticSocket.AddLight;
    using ScaleFlags = VF.Builder.Haptics.SpsTags.SocketScaleFlagsX;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

    internal class SpsSocketReceiverRadiusOffsetTest : SpsSenderBakeTestBase {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SocketReceiversUseDefaultRadiusOffsetWithoutMovingTheSender(bool radiusOffset, bool legacyOverride) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking.");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useRadiusOffset = radiusOffset;
            socket.overrideLegacyOffset = legacyOverride;
            socket.legacyOffsetLocal = new Vector3(.1f, .2f, .3f);
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            foreach (var self in new[] { false, true }) {
                bake.plugReceivers.Find(self, false);
                bake.plugReceivers.Find(self, true);
            }
            injector.GetService<SpsSocketReceiverService>().GetLegacyWidthReceiver(bake.plugReceivers).Find(false, false);
            injector.GetService<SpsChannelsService>().Apply(bake.channelContacts);
            var offset = radiusOffset ? Vector3.up * VRCFuryHapticSocketEditor.LegacyRadiusOffset : Vector3.zero;
            void CheckPositions() {
                var expected = bake.bakeRoot.TransformPoint(offset);
                foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true)) {
                    CheckVector(receiver.transform.TransformPoint(receiver.position), expected);
                }
                var sender = bake.worldSpace.Children().Single(obj => obj.name == "Sender - Root");
                CheckVector(sender.worldPosition, bake.bakeRoot.worldPosition);
            }
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(5));
            CheckPositions();
            root.transform.localScale *= 2;
            CheckPositions();
        }
    }
}
