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

    internal class SpsSocketMeasurementsServiceTest : SpsAvatarBuildTestBase {
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(2, true, true)]
        [TestCase(1, true, false)]
        [TestCase(2, false, false)]
        [TestCase(2, true, false, true)]
        [TestCase(0, false, true)]
        public void SocketFlagsAreConstantsMatchingTheWholePath(int type, bool hole, bool? shrink = null, bool? innerShrink = null) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
            socket.addLight = (VRCFuryHapticSocket.AddLight)type;
            AddStop(shrink);
            AddStop(innerShrink);
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var factory = injector.GetService<SpsSocketMeasurementsService>().GetMeasurements(bake, "Socket");
            foreach (var party in new[] { Party.Self, Party.Others, Party.Merged }) {
                Assert.That(factory(party)(SpsSocketIsHole).GetDefault(), Is.EqualTo(hole ? 1 : 0));
            }
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);

            void AddStop(bool? collapse) {
                if (collapse == null) return;
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                    transform = socket.addLight == VRCFuryHapticSocket.AddLight.None ? null : GameObjects.Create("Stop", root),
                    shrink = collapse.Value
                });
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MeasurementsOnlyGenerateLegacyMathWhenAPublicChannelIsAvailable(bool publicAlternative) {
            optimizeTrees = true;
            var outputs = Socket(false)(Party.Others);
            var depth = outputs(SpsDepthLocal);
            var radius = outputs(SpsPlugRadiusLocal);
            _ = outputs(SpsPlugEntryRadiusLocal);
            var socket = root.GetComponent<VRCFuryHapticSocket>();
            var receivers = injector.GetService<SpsSocketMeasurementsService>().GetReceivers(socket, root, "Test");
            var tree = receiverAnimator.GetTree(receivers);
            new BlendtreeMath(fx, tree).CopyInPlace(depth, "Injected depth");
            if (publicAlternative) {
                injector.GetService<SpsChannelsService>().Register(socket, new Lazy<VFBlendTreeDirect>(() => tree));
                injector.GetService<ChangeSpsChannelsService>().Request(new VF.Model.StateAction.ChangeSpsChannelsAction {
                    target = root,
                    channelSettings = new VF.Model.SpsChannelSettings { useSharedTag = true }
                });
            }
            Start();
            Assert.That(new AnimatorIterator.Trees().From(fx)
                .Any(t => t.BlendParameter?.EndsWith("/Native") == true), Is.EqualTo(publicAlternative));
            Assert.That(new AnimatorIterator.Trees().From(fx)
                .Any(t => t.BlendParameter?.EndsWith("/LegacyLengthValid") == true || t.BlendParameter?.EndsWith("/LegacyRadiusValid") == true
                    || t.BlendParameter?.EndsWith("/LegacyLengthSample") == true || t.BlendParameter?.EndsWith("/LegacyRadiusSample") == true),
                Is.EqualTo(publicAlternative));
            var legacyWriters = new AnimatorIterator.Clips().From(fx)
                .SelectMany(clip => clip.GetFloatBindings())
                .Select(binding => binding.propertyName)
                .Where(name => name.Contains("/Legacy") && (name.EndsWith("Estimate") || name.EndsWith("HelperRadius")))
                .Distinct();
            Assert.That(legacyWriters.Count(), Is.EqualTo(publicAlternative ? 3 : 0));
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>()
                .Single(r => r.allowOthers && r.collisionTags.Any(t => t.StartsWith(SpsTags.Prefix)));
            Set(receiver, 1, 0.2f, length: 0.4f, radius: 0.05f, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.2f).Within(0.001f));
            Assert.That(playable.GetFloat("Injected depth"), Is.EqualTo(-0.2f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SocketReciprocalResetsOnContactLossAndSettlesAfterImmediateReentry(bool optimize) {
            optimizeTrees = optimize;
            var outputs = Socket(false)(Party.Self);
            var depth = outputs(SpsDepthPlugLengths);
            var entryRadius = outputs(SpsPlugEntryRadiusMeters);
            var inverse = fx.parameters.Single(p => p.name.EndsWith("/Length/Inverted"));
            Start();
            var receiver = Main();
            Set(receiver, 1, 0.6f, length: 0.8f, radius: 0.02f);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.25f).Within(0.0001f));
            Assert.That(playable.GetFloat(entryRadius), Is.EqualTo(0.02f).Within(0.0001f));

            Set(receiver, 0, -1);
            Evaluate(1);
            Assert.That(playable.GetFloat(inverse.name), Is.EqualTo(1 / 0.3f).Within(0.0001f));
            Assert.That(playable.GetFloat(entryRadius), Is.Zero);

            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.04f);
            Evaluate(1);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-1).Within(0.0001f));
            Assert.That(playable.GetFloat(entryRadius), Is.Zero);
            Evaluate(1);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-1).Within(0.0001f));
            Assert.That(playable.GetFloat(entryRadius), Is.EqualTo(0.04f).Within(0.0001f));
            Evaluate(2);
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void NativeDepthDoesNotCreateAnUnusedLengthOutput() {
            _ = Socket(false)(Party.Self)(SpsDepthMeters);
            Assert.That(fx.parameters.Any(p => p.name.StartsWith("Test/Self/Length")), Is.False,
                "Native depth must read sender Z directly without generating an unused length output");
        }

        [Test]
        public void ReceiversAreLazyAndMeasurementsShareOneMainPerParty() {
            var factory = Socket();
            var self = factory(Party.Self);
            var others = factory(Party.Others);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
            _ = self(SpsPlugLengthMeters);
            _ = self(SpsDepthMeters);
            _ = self(SpsDepthPlugLengths);
            _ = self(SpsDepthLocal);
            Assert.That(self(SpsDepthLocal), Is.SameAs(self(SpsDepthLocal)));
            Assert.That(self(SpsDepthPlugLengths), Is.SameAs(self(SpsDepthPlugLengths)));
            Assert.That(self(SpsDepthLocal), Is.Not.SameAs(self(SpsDepthPlugLengths)));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
            _ = others(SpsDepthMeters);
            _ = self(SpsPlugRadiusMeters);
            _ = others(SpsPlugRadiusMeters);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(Main(true).collisionTags, Is.All.StartsWith(SpsTags.Prefix));
            Assert.That(Main(false).collisionTags, Does.Contain(SpsTags.LegacyPlugSphere));
            Assert.That(fx.parameters.Any(p => p.name.StartsWith("Test/Self/Legacy")), Is.False);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Any(r => r.collisionTags.Contains(SpsTags.LegacyPlugRoot)), Is.False);
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true)) {
                Assert.That(receiver.radius * receiver.transform.lossyScale.x, Is.EqualTo(0.2f).Within(0.0001f));
            }
        }

        [Test]
        public void MeasurementFactoriesRejectInvalidPartiesWithoutCreatingContacts() {
            var socket = Socket(false);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = socket((Party)999)(SpsDepthMeters));
            _ = socket(Party.Merged);
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var measurements = PlugMeasurements(plug);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = measurements((Party)999)(SpsDepthMeters));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

        [TestCase(0.7f, 0.4f, 0.05f)]
        [TestCase(0.1f, 0.4f, 0.05f)]
        [TestCase(0.002f, 0.4f, 0.05f)]
        [TestCase(2.5f, 4f, 0.5f)]
        public void NativeDimensionsAndEuclideanDepthRemainLiveIncludingSaturation(float distance, float length, float radius) {
            measurements = Socket(false)(Party.Self);
            var outputs = new[] { measurements(SpsPlugLengthMeters), measurements(SpsPlugRadiusMeters), measurements(SpsDepthMeters),
                measurements(SpsDepthPlugLengths), measurements(SpsDepthLocal),
                measurements(SpsPlugLengthLocal), measurements(SpsPlugRadiusLocal), measurements(SpsPlugRadiusPlugLengths) };
            Start();
            playable.SetFloat(scale, 2);
            Set(Main(), 1, distance, length, radius, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[0]), Is.EqualTo(length).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[1]), Is.EqualTo(radius).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(distance - length).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[3]), Is.EqualTo((distance - length) / length).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[4]), Is.EqualTo((distance - length) / 2).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[5]), Is.EqualTo(length / 2).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[6]), Is.EqualTo(radius / 2).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[7]), Is.EqualTo(radius / length).Within(0.002f));
            Set(Main(), 1, distance, length * 2, radius * 2, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[0]), Is.EqualTo(length * 2).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[1]), Is.EqualTo(radius * 2).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(distance - length * 2).Within(0.0001f));
            Assert.That(playable.GetFloat(outputs[5]), Is.EqualTo(length).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[6]), Is.EqualTo(radius).Within(0.002f));
            Assert.That(playable.GetFloat(outputs[7]), Is.EqualTo(radius / length).Within(0.002f));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(1));
            foreach (var idleSeparation in new[] { 0f, -1f }) {
                Set(Main(), 0, idleSeparation, length * 2, radius * 2, native: true);
                Evaluate();
                Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(100).Within(0.0001f));
            }
        }

        [TestCase(true, 0, 0.05f)]
        [TestCase(true, 0.4f, 0)]
        [TestCase(true, 0.4f, 0.4f)]
        [TestCase(false, 0, 0.05f)]
        [TestCase(false, 0.4f, 0)]
        [TestCase(false, 0.4f, 0.4f)]
        public void NativeTagsDetermineEncodingRegardlessOfDimensions(bool self, float length, float radius) {
            measurements = Socket()(self ? Party.Self : Party.Others);
            var depth = measurements(SpsDepthMeters);
            var lengthOutput = measurements(SpsPlugLengthMeters);
            var radiusOutput = measurements(SpsPlugRadiusMeters);
            Start();
            Set(Main(self), 0.8f, 1.2f, length, radius, native: true);
            var legacyTag = Main(self).parameter + "_CollisionTag_" + SpsTags.LegacyPlugSphere;
            if (fx.TryGetParam(legacyTag, out _)) playable.SetFloat(legacyTag, 1);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(1.2f - length).Within(0.001f));
            Assert.That(playable.GetFloat(lengthOutput), Is.EqualTo(length).Within(0.001f));
            Assert.That(playable.GetFloat(radiusOutput), Is.EqualTo(radius).Within(0.001f));
        }

        [TestCase(true, false, true)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, false)]
        [TestCase(false, true, false)]
        public void NativeTagSelectsEncodingOnlyWhenLegacyTagsCanMatch(bool self, bool includeLegacy, bool legacyAvailable) {
            injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { legacyModeAvailable = legacyAvailable }
            };
            measurements = Socket(includeLegacy)(self ? Party.Self : Party.Others);
            includeLegacy &= legacyAvailable;
            var depth = measurements(SpsDepthMeters);
            var length = measurements(SpsPlugLengthMeters);
            var radius = measurements(SpsPlugRadiusMeters);
            var velocity = measurements(SpsVelocityMeters);
            Start();
            Set(Main(self), 1, 0.2f, 0.4f, 0.05f, native: false);
            var legacyTag = Main(self).parameter + "_CollisionTag_" + SpsTags.LegacyPlugSphere;
            if (fx.TryGetParam(legacyTag, out _)) playable.SetFloat(legacyTag, 0);
            playable.SetFloat(Main(self).parameter + "_SenderVelocityZ", 0.6f);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(includeLegacy ? 0 : -0.2f).Within(0.001f));
            Assert.That(playable.GetFloat(length), Is.EqualTo(includeLegacy ? 0.2f : 0.4f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(includeLegacy ? 0.03f : 0.05f).Within(0.001f));
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(Main(self).advancedParameters.HasFlag(ContactReceiver.AdvancedParameter.CollisionTags), Is.EqualTo(!self && legacyAvailable));
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void NativePlugEntryRadiusUsesSampledRadiusAndSharesUnitConversions(bool self, bool merged) {
            var outputs = Socket(false)(merged ? Party.Merged : self ? Party.Self : Party.Others);
            var meters = outputs(SpsPlugEntryRadiusMeters);
            var local = outputs(SpsPlugEntryRadiusLocal);
            var normalized = outputs(SpsPlugEntryRadiusPlugLengths);
            Assert.That(outputs(SpsPlugEntryRadiusMeters), Is.SameAs(meters));
            Assert.That(fx.parameters.Count(p => p.name.Contains("/PlugEntryRadius") && !p.name.Contains("/Local")
                && !p.name.Contains("/PlugLengths")), Is.EqualTo(1));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Any(r => r.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.False);
            Start();
            playable.SetFloat(scale, 2);
            var receiver = merged ? Merged() : Main(self);
            Set(receiver, 1, 0.3f, length: 0.8f, radius: 0.12f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.04f);
            Evaluate();
            Assert.That(playable.GetFloat(meters), Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(playable.GetFloat(local), Is.EqualTo(0.02f).Within(0.001f));
            Assert.That(playable.GetFloat(normalized), Is.EqualTo(0.05f).Within(0.001f));
            foreach (var plugLength in new[] { 0.4f, 0.8f }) {
                foreach (var (outside, fraction) in new[] { (0.1f, 0f), (0.05f, 0f), (0.025f, 0.5f), (0f, 1f), (-0.1f, 1f) }) {
                    Set(receiver, 0.9f, plugLength * (1 + outside), length: plugLength, radius: 0.12f, native: true);
                    playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.04f);
                    Evaluate(30);
                    Assert.That(playable.GetFloat(meters), Is.EqualTo(0.04f * fraction).Within(0.001f),
                        $"Entry radius at {outside:P} outside a {plugLength}m plug");
                    Assert.That(playable.GetFloat(local), Is.EqualTo(0.02f * fraction).Within(0.001f));
                    Assert.That(playable.GetFloat(normalized), Is.EqualTo(0.04f * fraction / plugLength).Within(0.001f));
                }
            }
            Set(receiver, 0, -1, length: 0.8f, radius: 0.12f, native: true);
            Evaluate();
            foreach (var output in new[] { meters, local, normalized }) Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyPlugEntryRadiusTapersOverFirstFivePercentAndRequiresHelper(bool merged) {
            var outputs = Socket()(merged ? Party.Merged : Party.Others);
            var output = outputs(SpsPlugEntryRadiusMeters);
            _ = outputs(SpsDepthPlugLengths);
            Assert.That(fx.parameters.Count(p => p.name.Contains("/Dist/Meters/PlugLengths")), Is.EqualTo(1),
                "Radius taper and depth animations must share their normalized depth");
            Start();
            void Contact(float separation, float main, float helper) {
                Set(merged ? Merged() : Main(false), main, separation, length: 20, radius: 20);
                Set(Width(false), helper, separation);
                Evaluate(30);
            }
            Contact(0.3f, 1, 0);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "A fresh plug without a helper stays at zero");
            Contact(0.63f, 0.85f, 0.25f);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "Before insertion");
            Contact(0.6f, 1, 0.4f);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "Tip touching entrance");
            Contact(0.585f, 1, 0.475f);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.03f).Within(0.001f), "2.5% insertion");
            Contact(0.57f, 1, 0.55f);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.06f).Within(0.001f), "5% insertion");
            Contact(0.3f, 1, 1);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.06f).Within(0.001f), "Further insertion");
            var receiver = merged ? Merged() : Main(false);
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.04f);
            Evaluate(30);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.04f).Within(0.001f), "Native plugs use sampled radius even with a stale legacy helper");
            Contact(0.3f, 1, 0);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "A plug without a radius helper must not use the assumed radius");
            Contact(-1, 0, 0);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void LegacyLengthUsesCenterDistanceResetsOnExitAndIgnoresTeleports() {
            measurements = Socket()(Party.Others);
            var length = measurements(SpsPlugLengthMeters);
            var depth = measurements(SpsDepthMeters);
            Assert.That(fx.parameters.Any(p => p.name.Contains("LegacySourceStable") || p.name.EndsWith("_b")), Is.False);
            Start();
            Set(Main(false), 0.4f, 0.72f, length: 20, radius: 20, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(0.12f).Within(0.001f));
            Set(Main(false), 1, 0.4f, length: 20, radius: 20, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.2f).Within(0.001f));
            Set(Main(false), 0, -1);
            Evaluate();
            Set(Main(false), 1, 0.1f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.1f).Within(0.001f));
            Set(Main(false), 0.4f, 0.72f, z: 1.2f);
            Evaluate();
            Set(Main(false), 1, 0.1f, z: -1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Set(Main(false), 1, 0.1f, length: 0.4f, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.4f).Within(0.001f));
            Set(Main(false), 1, 0.1f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.1f).Within(0.001f),
                "Native contact must clear the previous legacy estimate's confidence");
        }

        [Test]
        public void LegacySizeEstimatesSkipIdleAndCaptureTheFirstSampleAfterNativeContact() {
            var outputs = Socket()(Party.Others);
            var length = outputs(SpsPlugLengthMeters);
            _ = outputs(SpsPlugRadiusMeters);
            var estimates = fx.parameters.Where(p => p.name.EndsWith("/LegacyLengthEstimate")
                || p.name.EndsWith("/LegacyHelperRadius") || p.name.EndsWith("/LegacyRadiusEstimate")).ToArray();
            Assert.That(estimates, Has.Length.EqualTo(3));
            Start();

            // A nearby width helper alone must not keep the estimates running.
            Set(Main(false), 0, -1);
            Set(Width(false), .6f, 1);
            Evaluate();
            foreach (var estimate in estimates) Assert.That(playable.GetFloat(estimate), Is.Zero);

            Set(Main(false), .8f, .8f, length: .7f, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(.7f).Within(.001f));

            Set(Main(false), .8f, .64f);
            Evaluate(1);
            Set(Main(false), 1, .4f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(.6f).Within(.001f),
                "Keep the one available unsaturated sample even when the previous contact was native");
        }

        [Test]
        public void LegacySizeValidationDoesNotDuplicateTheCacheFallback() {
            var outputs = Socket()(Party.Others);
            _ = outputs(SpsPlugLengthMeters);
            _ = outputs(SpsPlugRadiusMeters);
            var ranges = new AnimatorIterator.Trees().From(fx).Where(t => t.children.Count == 4
                && (t.BlendParameter?.Contains("LegacyLengthEstimate") == true
                    || t.BlendParameter?.Contains("LegacyRadiusEstimate") == true)).ToArray();
            Assert.That(ranges, Has.Length.EqualTo(2));
            foreach (var range in ranges) {
                Assert.That(new AnimatorIterator.Trees().From(range).Count, Is.EqualTo(1),
                    "The range check and sampled value should be written by one tree");
                Assert.That(range.children.All(child => child.motion is VFClip), Is.True);
            }
        }

        [Test]
        public void LegacyCacheChecksConfidenceOncePerSize() {
            var outputs = Socket()(Party.Others);
            _ = outputs(SpsPlugLengthMeters);
            _ = outputs(SpsPlugRadiusMeters);
            var confidence = fx.parameters.Where(p => p.name.EndsWith("/LegacyLengthValid")
                || p.name.EndsWith("/LegacyRadiusValid")).ToArray();
            Assert.That(confidence, Has.Length.EqualTo(2));
            foreach (var parameter in confidence) Assert.That(new AnimatorIterator.Trees().From(fx)
                .Count(t => t.BlendParameter == parameter.name), Is.EqualTo(1),
                "Choose the cached/uncached case once, before testing saturation and the sample");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyCachePreservesSampleRetainAndFallbackPriorityOnEveryFrame(bool optimize) {
            optimizeTrees = optimize;
            var outputs = Socket()(Party.Others);
            var length = outputs(SpsPlugLengthMeters);
            var radius = outputs(SpsPlugRadiusMeters);
            var lengthSample = fx.parameters.Single(p => p.name.EndsWith("/LegacyLengthSample"));
            var radiusSample = fx.parameters.Single(p => p.name.EndsWith("/LegacyRadiusSample"));
            var lengthConfidence = fx.parameters.Single(p => p.name.EndsWith("/LegacyLengthValid"));
            var radiusConfidence = fx.parameters.Single(p => p.name.EndsWith("/LegacyRadiusValid"));
            Start();
            var native = fx.parameters.Single(p => p.name.EndsWith("/Native"));
            var expectedLength = 0.3f;
            var expectedRadius = 0.03f;
            var lengthKnown = false;
            var radiusKnown = false;
            void Frame(float proximity, float separation, float helper, bool isNative = false) {
                Set(Main(false), proximity, separation, length: 0.7f, radius: 0.08f, native: isNative);
                Set(Width(false), helper, separation);
                // Model the prior policy using the inputs visible at this Animator evaluation.
                void Update(ref float value, ref bool known, VFAParam sample, bool width) {
                    var candidate = playable.GetFloat(sample);
                    var assumed = width ? 0.03f : 0.3f;
                    if (separation <= 0) { value = assumed; known = false; }
                    else if (playable.GetFloat(native) > 0) { value = width ? 0.08f : 0.7f; known = false; }
                    else if (width && helper <= 0) { value = assumed; known = false; }
                    else if (candidate > 0 && (proximity < 1 || !known)) { value = candidate; known = true; }
                    else if (!(known && (proximity >= 1 || width && helper >= 1))) { value = assumed; known = false; }
                }
                Update(ref expectedLength, ref lengthKnown, lengthSample, false);
                Update(ref expectedRadius, ref radiusKnown, radiusSample, true);
                Evaluate(1);
                Assert.That(playable.GetFloat(length), Is.EqualTo(expectedLength).Within(0.00001f));
                Assert.That(playable.GetFloat(radius), Is.EqualTo(expectedRadius).Within(0.00001f));
                Assert.That(playable.GetFloat(lengthConfidence), Is.EqualTo(lengthKnown ? 1 : 0));
                Assert.That(playable.GetFloat(radiusConfidence), Is.EqualTo(radiusKnown ? 1 : 0));
            }
            for (var i = 0; i < 12; i++) Frame(0.8f, 0.8f, 0.68f);
            // A valid pending radius sample must still win when the helper first saturates.
            Frame(0.8f, 0.8f, 0.64f);
            Frame(0.8f, 0.8f, 0.64f);
            Frame(0.8f, 0.8f, 1);
            foreach (var proximity in new[] { 0f, 0.8f, VRCFuryEditorUtils.NextFloatDown(1), 1f, VRCFuryEditorUtils.NextFloatUp(1) })
                foreach (var helper in new[] { 0f, 0.7f, 1f })
                    foreach (var separation in new[] { -1f, 0.1f, 0.8f, 4f })
                        foreach (var isNative in new[] { false, true }) Frame(proximity, separation, helper, isNative);
        }

        [Test]
        public void LegacySizeSamplesKeepValidationAndValueTogetherWhileDepthAndVelocityStayLive() {
            var outputs = Socket()(Party.Others);
            var length = outputs(SpsPlugLengthMeters);
            var radius = outputs(SpsPlugRadiusMeters);
            var depth = outputs(SpsDepthMeters);
            var velocity = outputs(SpsVelocityMeters);
            Start();
            var samples = new (float distance, float helperDistance, float helper)[] {
                (0.64f, 0.64f, 0.2f),
                (0.84f, 0.84f, 0.4f),
                (4f, 4f, 0.2f),
                (0.03f, 0.03f, 0.2f),
                (0.64f, 0.64f, 0.9f),
                (0.64f, 0.168f, 0.21f),
                (0.64f, -1f, 0f)
            };
            for (var frame = 0; frame < 70; frame++) {
                var sample = samples[frame % samples.Length];
                Set(Main(false), 0.8f, sample.distance);
                Set(Width(false), sample.helper, sample.helperDistance);
                var speed = frame % 2 == 0 ? -0.4f : 0.7f;
                playable.SetFloat(Main(false).parameter + "_SenderVelocityZ", speed);
                Evaluate(1);
                Assert.That(new[] { 0.3f, 0.6f, 0.8f }.Any(v => Mathf.Abs(v - playable.GetFloat(length)) < 0.001f), Is.True,
                    "A stale validity decision must not accept a negative or oversized length");
                Assert.That(new[] { 0.03f, 0.04f, 0.06f }.Any(v => Mathf.Abs(v - playable.GetFloat(radius)) < 0.001f), Is.True,
                    "A stale validity decision must not accept a negative radius or the helper's minimum-radius artifact");
                Assert.That(playable.GetFloat(depth), Is.EqualTo(0.04f).Within(0.001f));
                Assert.That(playable.GetFloat(velocity), Is.EqualTo(speed).Within(0.001f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacySizesSeedFromSaturatedContactAndKeepBetterMeasurements(bool useMerged) {
            measurements = Socket()(useMerged ? Party.Merged : Party.Others);
            var length = measurements(SpsPlugLengthMeters);
            var radius = measurements(SpsPlugRadiusMeters);
            var depth = measurements(SpsDepthMeters);
            Start();
            void Contact(float distance, float main, float helper) {
                Set(useMerged ? Merged() : Main(false), main, distance, length: 20, radius: 20, z: distance);
                Set(Width(false), helper, distance, z: distance);
                Evaluate();
            }
            Contact(0.58f, 1, 0.5f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.58f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
            Contact(0.48f, 1, 1);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.58f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.1f).Within(0.001f));
            Contact(0.62f, 0.9f, 0.3f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Contact(0.5f, 1, 0.9f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
        }

        [Test]
        public void TwentyCentimeterReceiverCanMeasureALongerLegacyPlug() {
            measurements = Socket()(Party.Others);
            var length = measurements(SpsPlugLengthMeters);
            var depth = measurements(SpsDepthMeters);
            Start();
            Set(Main(false), 0.6f, 1.28f, z: 1.28f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(0.08f).Within(0.001f));
        }

        [Test]
        public void LegacyWidthTrustsHelperRegardlessOfCenterButRejectsHelperFloor() {
            measurements = Socket()(Party.Others);
            var radius = measurements(SpsPlugRadiusMeters);
            Assert.That(fx.parameters.Any(p => p.name.Contains("LegacyWidthMatched") || p.name.Contains("Difference")), Is.False);
            Start();
            Set(Main(false), 0.8f, 0.8f, x: -0.4f, z: 1.2f);
            Set(Width(false), 0.2f, 0.8f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.2f, 0.8f, x: 0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.21f, 0.168f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.03f).Within(0.001f));
            Set(Width(false), 0.2f, 0.8f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.21f, 0.168f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.03f).Within(0.001f));
        }

        [Test]
        public void SocketCombinedMeasurementsUseMergedReceiverRatherThanComparingDepth() {
            var contacts = Socket(false)(Party.Merged);
            var depth = contacts(SpsDepthMeters);
            var length = contacts(SpsPlugLengthMeters);
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Closest/Is")), Is.False);
            Start();
            Set(Main(true), 1, 0.6f, 1, 0.1f, native: true);
            Set(Main(false), 1, 0.1f, 0.2f, 0.02f, native: true);
            Set(Merged(), 1, 0.1f, 0.2f, 0.02f, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.1f).Within(0.001f));
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void NativeVelocityUsesOnlyZAndClearsOnExit() {
            var factory = Socket(false);
            var contacts = factory(Party.Merged);
            var velocity = contacts(SpsVelocityMeters);
            var localVelocity = contacts(SpsVelocityLocal);
            var normalizedVelocity = contacts(SpsVelocityPlugLengths);
            Assert.That(fx.parameters.Any(p => p.name.Contains("LatchedDiff") || p.name.Contains("timeSinceLoad")), Is.False);
            Start();
            playable.SetFloat(scale, 2);
            Set(Merged(), 1, 0.4f, 0.4f, 0.05f, native: true);
            Set(Main(false), 0, -1);
            playable.SetFloat(Merged().parameter + "_SenderVelocityX", 3);
            playable.SetFloat(Merged().parameter + "_SenderVelocityY", 4);
            playable.SetFloat(Merged().parameter + "_SenderVelocityMagnitude", 5);
            playable.SetFloat(Merged().parameter + "_SenderVelocityZ", -0.3f);
            Evaluate();
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(-0.3f).Within(0.001f));
            Assert.That(playable.GetFloat(localVelocity), Is.EqualTo(-0.15f).Within(0.002f));
            Assert.That(playable.GetFloat(normalizedVelocity), Is.EqualTo(-0.75f).Within(0.002f));
            playable.SetFloat(Merged().parameter + "_SenderVelocityZ", 0.8f);
            Evaluate(1);
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0.8f).Within(0.001f), "Z changes must not pass through an AAP history pipeline");
            playable.SetFloat(Merged().parameter + "_SenderVelocityZ", 0);
            Evaluate(1);
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0).Within(0.001f));
            Set(Merged(), 0, -1);
            playable.SetFloat(Merged().parameter + "_SenderVelocityZ", 10);
            Evaluate();
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(0).Within(0.005f));
            Set(Main(false), 1, 0.1f, 0.4f, 0.05f, native: true);
            Set(Merged(), 1, 0.1f, 0.4f, 0.05f, native: true);
            playable.SetFloat(Main(false).parameter + "_SenderVelocityZ", -1.2f);
            playable.SetFloat(Merged().parameter + "_SenderVelocityZ", -1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(velocity), Is.EqualTo(-1.2f).Within(0.001f));
        }
    }
}
#endif
