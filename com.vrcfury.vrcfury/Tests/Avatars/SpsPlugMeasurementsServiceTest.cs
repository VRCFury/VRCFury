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

    internal class SpsPlugMeasurementsServiceTest : SpsAvatarBuildTestBase {
        [Test]
        public void PlugSocketFlagsDecodeNativeScaleAndLegacyTags(
            [Values(true, false)] bool shared, [Values(0, 1, 2)] int partyIndex) {
            var party = (Party)partyIndex;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            plug.channelSettings.useSharedTag = shared;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test", allowSelf = true, allowOthers = true });
            var outputs = PlugMeasurements(plug)(party);
            var hole = outputs(SpsSocketIsHole);
            var isLegacy = outputs(SpsInteractingWithLegacy);
            Assert.That(outputs(SpsSocketIsHole), Is.SameAs(hole));
            var flagTrees = new AnimatorIterator.Trees().From(fx).Where(t => t.name.EndsWith("/NativeFlag")).ToArray();
            Assert.That(flagTrees, Has.Length.EqualTo(1));
            Assert.That(flagTrees.Single().children, Has.Count.EqualTo(2));
            Assert.That(flagTrees.All(t => t.children.All(child => child.motion is VFClip)), Is.True,
                "Each native socket flag must decode directly to clips in a single 1D tree");
            Start();
            var receiver = party == Party.Merged ? Merged() : root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Single(r => r.allowSelf == (party == Party.Self));
            foreach (var packed in new[] { 1, 8191, 8192, 16383, 16384, 24575, 24576, 32767 }) {
                Set(receiver, 0.9f, 0.2f, radius: packed, native: true);
                Evaluate();
                Assert.That(playable.GetFloat(hole), Is.EqualTo((packed & (int)SpsTags.SocketScaleFlagsX.Collapse) != 0 ? 1 : 0));
                Assert.That(playable.GetFloat(isLegacy), Is.Zero);
            }
            const int pathHole = (int)SpsTags.SocketScaleFlagsZ.AnimatorHole | 1;
            Set(receiver, 0.9f, 0.2f, length: pathHole, radius: 1, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(hole), Is.EqualTo(1));
            Set(receiver, 0.9f, 0.2f, length: (int)SpsTags.SocketScaleFlagsZ.AnimatorHole - 1, radius: 1, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(hole), Is.Zero);
            if (shared && party != Party.Self) {
                foreach (var (holeTag, ringTag) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) {
                    Set(receiver, 0.9f, 0.2f, length: pathHole, radius: 32767, native: false);
                    playable.SetFloat(receiver.parameter + "_CollisionTag_" + SpsTags.LegacySocketHole, holeTag);
                    playable.SetFloat(receiver.parameter + "_CollisionTag_" + SpsTags.LegacySocketRing, ringTag);
                    Evaluate();
                    Assert.That(playable.GetFloat(hole), Is.EqualTo(holeTag == 1 && ringTag == 0 ? 1 : 0));
                    Assert.That(playable.GetFloat(isLegacy), Is.EqualTo(1));
                }
            }
            Set(receiver, 0, -1, length: pathHole, radius: 32767, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(hole), Is.Zero);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(0.3f)]
        public void PlugDepthUsesItsOwnResizedDimensionsAndResetsWithNoContact(float idleSeparation) {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test", allowSelf = true, allowOthers = true });
            measurements = PlugMeasurements(plug)(Party.Self);
            var outputs = new[] { measurements(SpsPlugLengthMeters), measurements(SpsPlugRadiusMeters),
                measurements(SpsDepthMeters), measurements(SpsDepthLocal) };
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single();
            Assert.That(receiver.collisionTags, Does.Contain(SpsTags.FormatSocket(SelfTag("test"))));
            Assert.That(receiver.collisionTags.Any(tag => tag.StartsWith("SPS/SocketPath/")), Is.False);
            Assert.That(receiver.radius * receiver.transform.lossyScale.x, Is.EqualTo(0.64f).Within(0.0001f));
            Start();
            playable.SetFloat(scale, 2);
            Set(receiver, 0.9f, 0.3f, length: 1, radius: 32767, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[0]), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(playable.GetFloat(outputs[1]), Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(playable.GetFloat(outputs[3]), Is.EqualTo(-0.25f).Within(0.002f));
            Set(receiver, 0, idleSeparation);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(100).Within(0.001f));
        }

        [Test]
        public void PlugMeasurementsUseCalibratedRawScale() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = true;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            frame.localScale = Vector3.one * 2;
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            var bake = new VRCFuryHapticPlugEditor.BakeResult {
                bakeRoot = frame,
                socketReceivers = receivers,
                localLength = 0.4f,
                localRadius = 0.05f
            };
            var detector = injector.GetService<WorldScaleDetectorService>();
            var raw = detector.GetRawWorldScale(frame, "Test");
            var measurements = injector.GetService<SpsPlugMeasurementsService>().GetMeasurements(bake, "Test")(Party.Self);
            var length = measurements(SpsPlugLengthMeters);
            var radius = measurements(SpsPlugRadiusMeters);
            var depth = measurements(SpsDepthMeters);
            var source = receiverAnimator.GetParameter(receivers, Party.Self);
            Start();
            var rawScaleBindings = new AnimatorIterator.Trees().From(fx)
                .SelectMany(tree => tree.children)
                .Where(child => child.directBlendParameter == raw.name)
                .SelectMany(child => new AnimatorIterator.Clips().From(child.motion))
                .SelectMany(clip => clip.GetFloatBindings())
                .Select(binding => binding.propertyName)
                .ToArray();
            Assert.That(rawScaleBindings, Is.EquivalentTo(new[] { length.name, radius.name, depth.name }));
            var contact = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == source.name);
            Set(contact, 0.5f, 0.5f);
            Evaluate();
            playable.SetFloat(raw, 0.02f);
            Evaluate(1);
            Assert.That(playable.GetFloat(length), Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-1.1f).Within(0.0001f));
        }

        [Test]
        public void CombinedPlugDepthReadsMergedSdkOutputsWithoutRebuildingPartySelection() {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var factory = PlugMeasurements(plug);
            var contacts = factory(Party.Merged);
            var self = factory(Party.Self);
            var others = factory(Party.Others);
            Assert.That(self(SpsPlugLengthMeters), Is.SameAs(others(SpsPlugLengthMeters)));
            Assert.That(self(SpsPlugRadiusMeters), Is.SameAs(others(SpsPlugRadiusMeters)));
            var depth = contacts(SpsDepthMeters);
            var velocity = contacts(SpsVelocityMeters);
            var normalizedDepth = contacts(SpsDepthPlugLengths);
            var normalizedVelocity = contacts(SpsVelocityPlugLengths);
            var othersDepth = others(SpsDepthMeters);
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            var mergedName = receiverAnimator.GetParameter(receivers, Party.Merged).name;
            var otherName = receiverAnimator.GetParameter(receivers, false).name;
            Start();
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            var merged = all.First(r => r.parameter == mergedName);
            var other = all.Single(r => r.parameter == otherName);
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Closest/IsSelf") || p.name.Contains("/Closest/IsOthers")), Is.False);
            Set(other, 0.9f, 0.7f);
            Set(merged, 0.8f, 0.1f);
            playable.SetFloat(mergedName + "_SenderVelocityZ", -0.8f);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.3f).Within(0.001f));
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(-0.8f).Within(0.001f));
            Assert.That(playable.GetFloat(normalizedDepth), Is.EqualTo(-0.75f).Within(0.002f));
            Assert.That(playable.GetFloat(normalizedVelocity), Is.EqualTo(-2).Within(0.002f));
            Assert.That(playable.GetFloat(othersDepth), Is.EqualTo(0.3f).Within(0.001f));
            playable.SetFloat(mergedName + "_PointSeparation", 0.3f);
            playable.SetFloat(mergedName + "_SenderVelocityZ", 0.4f);
            Evaluate(1);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.1f).Within(0.001f));
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(playable.GetFloat(normalizedDepth), Is.EqualTo(-0.25f).Within(0.002f));
            Assert.That(playable.GetFloat(normalizedVelocity), Is.EqualTo(1).Within(0.002f));
            Assert.That(playable.GetFloat(othersDepth), Is.EqualTo(0.3f).Within(0.001f));
            playable.SetFloat(scale, 2);
            Evaluate(1);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.5f).Within(0.001f),
                "Plug depth must not wait for an intermediate length AAP after a scale change");
            Evaluate(2);
            Assert.That(playable.GetFloat(normalizedDepth), Is.EqualTo(-0.625f).Within(0.002f));
            Assert.That(playable.GetFloat(normalizedVelocity), Is.EqualTo(0.5f).Within(0.002f));
            Set(merged, 0, -1);
            Evaluate(1);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(100).Within(0.001f));
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0).Within(0.001f));
        }
    }
}
#endif
