using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;
using Flags = VF.Component.VRCFuryHapticSocket.AddLight;
using ScaleFlags = VF.Builder.Haptics.HapticUtils.SocketScaleFlags;

namespace VF.Tests {
    [Category("VRCFury")]
    public class Sps3SendersTest {
        private GameObject root;
        private VRCFuryInjector injector;

        [SetUp]
        public void SetUp() {
            root = new GameObject("SPS3 Test");
            root.transform.position = new Vector3(1, 2, 3);
            root.transform.rotation = Quaternion.Euler(31, 47, 13);
            root.transform.localScale = Vector3.one * 2;
            injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FormatsNativeSenderTags() {
            Assert.That(SpsTags.FormatPlug("_nothips"), Is.EqualTo("SPS/Plug/_nothips"));
            Assert.That(SpsTags.FormatSocket("_public"), Is.EqualTo("SPS/Socket/_public"));
            Assert.That(SpsTags.FormatSocketPath("custom"), Is.EqualTo("SPS/SocketPath/custom"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangeSpsTagActionNoLongerGeneratesAnimation(bool debugMode) {
            var builder = new VF.Actions.ChangeSpsTagActionBuilder();
            var clip = builder.Build(new VF.Model.StateAction.ChangeSpsTagAction { target = root.transform }, debugMode);
            Assert.That(clip.GetFloatBindings(), Is.Empty);
        }

        [Test]
        public void ScaleProtocolRoundTripsEveryGroupAndPathIndex() {
            foreach (var scale in new[] { 0.001f, 0.1f, 1, 100f }) {
                for (var group = 1; group <= HapticUtils.GroupCount; group++) {
                    for (var pathIndex = 0; pathIndex < 8; pathIndex++) {
                        for (var flagBits = 0; flagBits < 32; flagBits++) {
                            var encoded = HapticUtils.EncodeSocketScale(group, pathIndex, (ScaleFlags)(flagBits << 10)) * scale;
                            var packed = Mathf.RoundToInt(encoded.x / encoded.z);
                            Assert.That(packed & 127, Is.EqualTo(group));
                            Assert.That((packed >> 7) & 7, Is.EqualTo(pathIndex));
                            Assert.That((packed >> 10) & 31, Is.EqualTo(flagBits));
                            Assert.That(encoded.y / encoded.z, Is.EqualTo(0.0021f).Within(0.0000001));
                        }
                    }
                }
            }
        }

        [Test]
        public void ScaleProtocolKeepsGroupAndIndexSeparateFromCollisionFlags() {
            Assert.That((int)ScaleFlags.UnlockAll, Is.EqualTo(1 << 10));
            Assert.That((int)ScaleFlags.UnlockLocalX, Is.EqualTo(1 << 11));
            Assert.That((int)ScaleFlags.RadiusOffset, Is.EqualTo(1 << 12));
            Assert.That((int)ScaleFlags.Twoway, Is.EqualTo(1 << 13));
            Assert.That((int)ScaleFlags.Collapse, Is.EqualTo(1 << 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => HapticUtils.EncodeSocketScale(1, 0, (ScaleFlags)1));
            Assert.That(HapticUtils.EncodeSocketScale(1, 0), Is.EqualTo(new Vector3(1, 0.0021f, 1)));
            Assert.That(HapticUtils.EncodeSocketScale(37, 2), Is.EqualTo(new Vector3(293, 0.0021f, 1)));
            Assert.That(HapticUtils.EncodeSocketScale(127, 7), Is.EqualTo(new Vector3(1023, 0.0021f, 1)));
            var allFlags = ScaleFlags.Collapse | ScaleFlags.Twoway | ScaleFlags.RadiusOffset | ScaleFlags.UnlockLocalX | ScaleFlags.UnlockAll;
            Assert.That(HapticUtils.EncodeSocketScale(127, 7, allFlags), Is.EqualTo(new Vector3(32767, 0.0021f, 1)));
            var sender = GameObjects.Create("Encoded", root);
            sender.worldScale = HapticUtils.EncodeSocketScale(127, 7, allFlags);
            root.transform.localScale *= 3;
            var matrix = sender.localToWorldMatrix;
            CheckVector(matrix.GetColumn(0).normalized, root.transform.right);
            CheckVector(matrix.GetColumn(1).normalized, root.transform.up);
            CheckVector(matrix.GetColumn(2).normalized, root.transform.forward);
            var normalization = matrix.GetColumn(2).magnitude;
            Assert.That(Mathf.RoundToInt(matrix.GetColumn(0).magnitude / normalization), Is.EqualTo(32767));
            Assert.That(matrix.GetColumn(1).magnitude / normalization, Is.EqualTo(0.0021f).Within(0.0000001));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(128, 0)]
        [TestCase(1, -1)]
        [TestCase(1, 8)]
        public void RejectsUnrepresentableProtocolValues(int group, int pathIndex) {
            Assert.Throws<ArgumentOutOfRangeException>(() => HapticUtils.EncodeSocketScale(group, pathIndex));
        }

        [Test]
        public void PlugBakerReusesLengthSenderAndPreservesLegacyGeometry() {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var contacts = root.GetComponentsInChildren<VRCContactSender>(true);
            if (!BuildTargetUtils.IsDesktop()) {
                Assert.That(contacts, Is.Empty);
                return;
            }

            Assert.That(contacts.Length, Is.EqualTo(4));
            var main = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugSphere));
            var width = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugWidth));
            var envelope = contacts.Single(c => c.collisionTags.Contains(SpsTags.PlugClose));
            var legacyRoot = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugRoot));
            Assert.That(main.collisionTags, Does.Contain(SpsTags.FormatPlug("_public")));
            Assert.That(main.collisionTags, Does.Contain("SPS/Flag/ScaleV1"));
            Assert.That(envelope.collisionTags, Is.EqualTo(new[] { SpsTags.PlugClose }));
            foreach (var tag in new[] { SpsTags.LegacyPlugWidth,
                         SpsTags.LegacyPlugRoot }) {
                Assert.That(contacts.Single(c => c.collisionTags.Contains(tag)).collisionTags,
                    Is.EquivalentTo(new[] { tag, tag + "_SelfNotOnHips" }));
                Assert.That(contacts.Single(c => c.collisionTags.Contains(tag)).collisionTags,
                    Does.Not.Contain(SpsTags.ScaleV1));
            }
            Assert.That(contacts.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("SPSN1/Legacy")), Is.False);
            Assert.That(main.radius * MaxScale(main.transform), Is.EqualTo(0.4f).Within(0.00001));
            Assert.That(width.radius * MaxScale(width.transform), Is.EqualTo(0.34f).Within(0.00001));
            Assert.That(envelope.radius * MaxScale(envelope.transform), Is.EqualTo(0.03f).Within(0.00001));
            Assert.That(legacyRoot.radius * MaxScale(legacyRoot.transform), Is.EqualTo(0.01f).Within(0.00001));
            Assert.That(envelope.position.z * envelope.transform.lossyScale.z, Is.EqualTo(0.2f).Within(0.00001));

            var native = main;
            Assert.That(native.radius, Is.EqualTo(1).Within(0.000001));
            Assert.That(native.collisionTags, Does.Contain(SpsTags.LegacyPlugSphere + "_SelfNotOnHips"));
            Assert.That(native.collisionTags.Any(t => t.StartsWith("SPSN1/Plug/")), Is.False);
            Assert.That(native.position, Is.EqualTo(Vector3.zero));
            CheckVector(native.transform.lossyScale, new Vector3(0.03f, 0.0021f, 0.4f));
            var yConstraint = VFConstraint.GetConstraintsInSelfAndChildren(new[] { native.owner() }).Single();
            Assert.That(yConstraint.IsScale(), Is.True);
            Assert.That(yConstraint.GetFirstSource().name, Is.EqualTo("WorldSpace"));
            Assert.That(yConstraint.GetWeight(0), Is.EqualTo(1));
            var component = yConstraint.GetComponent();
            if (component is UnityEngine.Animations.ScaleConstraint unityConstraint) {
                Assert.That(unityConstraint.scalingAxis, Is.EqualTo(UnityEngine.Animations.Axis.Y));
                Assert.That(unityConstraint.scaleOffset.y, Is.EqualTo(0.0021f));
            } else {
                Assert.That(component.GetType().VFField("AffectsScaleX").GetValue(component), Is.False);
                Assert.That(component.GetType().VFField("AffectsScaleY").GetValue(component), Is.True);
                Assert.That(component.GetType().VFField("AffectsScaleZ").GetValue(component), Is.False);
                Assert.That(((Vector3)component.GetType().VFField("ScaleOffset").GetValue(component)).y, Is.EqualTo(0.0021f));
            }
            CheckVector(native.transform.position, root.transform.position);
            CheckVector(native.transform.forward, root.transform.forward);
            root.transform.localScale *= 3;
            // Native constraints are not evaluated by a synchronous EditMode transform assignment.
            Assert.That(native.transform.lossyScale.x, Is.EqualTo(0.09f).Within(0.00001));
            Assert.That(native.transform.lossyScale.z, Is.EqualTo(1.2f).Within(0.00001));
            Assert.That(main.radius * MaxScale(main.transform), Is.EqualTo(1.2f).Within(0.00001));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

        [TestCase((int)VRCFuryHapticSocket.AddLight.Hole, (int)Flags.Hole)]
        [TestCase((int)VRCFuryHapticSocket.AddLight.Ring, (int)Flags.Ring)]
        [TestCase((int)VRCFuryHapticSocket.AddLight.RingOneWay, (int)Flags.RingOneWay)]
        public void SocketBakerKeepsLightsAndLegacyContactsWithoutMarkers(int typeValue, int flagValue) {
            var type = (VRCFuryHapticSocket.AddLight)typeValue;
            var flags = (Flags)flagValue;
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = type;
            socket.useRadiusOffset = true;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            if (!BuildTargetUtils.IsDesktop()) {
                Assert.That(NativeSocketSenders(result.bakeRoot), Is.Empty);
                return;
            }

            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Animator>(), Is.Empty);
            var lights = result.lights.GetComponentsInSelfAndChildren<Light>();
            Assert.That(lights.Select(l => l.range), Is.EquivalentTo(new[] {
                type == VRCFuryHapticSocket.AddLight.Hole ? 0.4106f : 0.4206f, 0.4506f
            }));
            var legacy = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>();
            Assert.That(legacy.Length, Is.EqualTo(1));
            CheckVector(legacy.Single(c => c.collisionTags.Contains(SpsTags.LegacySocket2)).transform.position,
                result.bakeRoot.worldPosition);
            Assert.That(legacy.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("SPSN1/Legacy")), Is.False);
            var contact = NativeSocketSenders(result.bakeRoot).Single();
            CheckNode(contact, 1, 0, flags, radiusOffset: true);
            Assert.That(contact, Is.SameAs(legacy.Single(c => c.collisionTags.Contains(SpsTags.LegacySocket2))));
            Assert.That(contact.transform.parent.asVf(), Is.EqualTo(result.senders));
            Assert.That(result.senders.parent, Is.EqualTo(result.worldSpace));
            var typeBits = flags == Flags.Hole ? 1 << 14 : flags == Flags.Ring ? 1 << 13 : 0;
            Assert.That(contact.transform.lossyScale.x, Is.EqualTo(1 | typeBits | (1 << 12)).Within(0.01));
            Assert.That(legacy.All(c => c.radius == 0), Is.True);
            Assert.That(legacy.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("TPS_Orf_Norm")), Is.False);
            CheckVector(contact.transform.position, result.bakeRoot.worldPosition);
            CheckVector(contact.transform.forward, result.bakeRoot.TransformDirection(Vector3.forward));
            Assert.That(Vector3.Distance(contact.transform.position, lights.First().transform.position), Is.GreaterThan(0.05));
            root.transform.localScale *= 3;
            CheckNode(contact, 1, 0, flags, 3, radiusOffset: true);
            result.senders.active = false;
            Assert.That(contact.gameObject.activeInHierarchy, Is.False);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

        [Test]
        public void GuidedRecordsKeepOrderShrinkTerminalAndStopTransforms() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Ring;
            socket.useLights = false;
            socket.tags.Add("  CuStOm  ");
            for (var i = 0; i < 3; i++) {
                var stop = new GameObject("Guide " + i).transform;
                stop.SetParent(root.transform, false);
                stop.localPosition = new Vector3(i + 1, i * 0.1f, 0.2f);
                stop.localRotation = Quaternion.Euler(i * 20, 10, 30);
                stop.localScale = Vector3.one * (i + 1);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                    transform = stop, shrink = i != 1
                });
            }
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            if (!BuildTargetUtils.IsDesktop()) return;
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            var contacts = NativeSocketSenders(result.bakeRoot);
            Assert.That(contacts.Length, Is.EqualTo(4));
            CheckNode(contacts[0], 1, 0, Flags.Hole);
            CheckNode(contacts[1], 1, 1, Flags.RingOneWay);
            CheckNode(contacts[2], 1, 2, Flags.Hole);
            CheckNode(contacts[3], 1, 3, Flags.RingOneWay);
            for (var i = 1; i < contacts.Length; i++) {
                var target = socket.guidedPathStops[i - 1].transform;
                CheckVector(contacts[i].transform.position, target.position);
                CheckVector(contacts[i].transform.forward, target.forward);
                Assert.That(contacts[i].transform.lossyScale.x, Is.EqualTo(1 | (i << 7) | (i == 2 ? 1 << 14 : 0)).Within(0.01));
                Assert.That(contacts[i].radius, Is.Zero);
                Assert.That(contacts[i].transform.parent.asVf(), Is.EqualTo(result.senders));
                var constraints = VFConstraint.GetConstraintsInSelfAndChildren(new[] { result.bakeRoot })
                    .Where(c => c.GetSources().Single() == target.asVf()).ToArray();
                Assert.That(constraints.Length, Is.EqualTo(1));
                Assert.That(constraints.All(c => c.GetSources().Single() == target.asVf()), Is.True);
                Assert.That(contacts[i].collisionTags, Does.Contain("SPS/SocketPath/custom"));
                Assert.That(contacts[i].collisionTags, Does.Contain("SPS/SocketPath/_nothips"));
            }
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>().Length, Is.EqualTo(4));
            Assert.That(VFConstraint.GetConstraintsInSelfAndChildren(new[] { result.bakeRoot }).Count(), Is.EqualTo(4));
            result.senders.active = false;
            Assert.That(contacts.All(c => !c.gameObject.activeInHierarchy), Is.True);
        }

        [Test]
        public void EachBakeGetsAUniqueGroupId() {
            if (!BuildTargetUtils.IsDesktop()) return;
            var service = injector.GetService<VRCFuryHapticSocketBaker>();
            var socketA = root.AddComponent<VRCFuryHapticSocket>();
            var other = new GameObject("Other");
            other.transform.SetParent(root.transform, false);
            var socketB = other.AddComponent<VRCFuryHapticSocket>();
            CheckNode(ConfigureSocket(service, root, socketA), 1, 0, Flags.Hole);
            CheckNode(ConfigureSocket(service, other, socketB), 2, 0, Flags.Hole);
            CheckNode(ConfigureSocket(service, root, socketA), 3, 0, Flags.Hole);
        }

        [Test]
        public void GuideMetadataScaleIsIndependentOfAuthoredStopScale() {
            if (!BuildTargetUtils.IsDesktop()) return;
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            var stop = new GameObject("Stop").transform;
            stop.SetParent(root.transform, false);
            stop.localScale = Vector3.one * 3;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var guide = NativeSocketSenders(result.bakeRoot).Last();
            var expected = new Vector3(129 | (1 << 14), 0.0021f, 1);
            CheckVector(guide.transform.lossyScale, expected);
            stop.localScale *= 10;
            CheckVector(guide.transform.lossyScale, expected);
            Assert.That(guide.transform.lossyScale.y, Is.GreaterThan(0.002f).And.LessThan(0.003f));
            var constraint = VFConstraint.GetConstraintsInSelfAndChildren(new[] { guide.owner() }).Single();
            Assert.That(constraint.GetSources().Single(), Is.EqualTo(stop.asVf()));
            result.senders.active = false;
            Assert.That(guide.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void RoutingPreservesSharedOptOutNormalizationAndHipVariants() {
            if (!BuildTargetUtils.IsDesktop()) return;
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.useSharedTag = false;
            var service = injector.GetService<VRCFuryHapticSocketBaker>();
            Assert.That(NativeSocketSenders(ConfigureSocket(service, root, socket).owner()), Is.Empty);
            socket.tags.Add("  CuStOm ");
            socket.tags.Add("custom");
            var tags = ConfigureSocket(service, root, socket).collisionTags;
            var tag = "SPS/Socket/custom";
            Assert.That(tags.Where(t => t.StartsWith("SPS/Socket/")), Is.EquivalentTo(new[] { tag }));
            tags = ConfigureSocket(service, root, socket, true).collisionTags;
            Assert.That(tags.Where(t => t.StartsWith("SPS/Socket/")), Is.EquivalentTo(new[] { tag }));
        }

        [Test]
        public void ContactTagsUseConfiguredChannels() {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(HumanBodyBones.LeftHand));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.tags.AddRange(new[] { "  My Channel  ", "Other", "Ignored Third" });
            var configurer = injector.GetService<SpsConfigurer>();
            var expected = new[] { "my channel", "other", "hand", "handleft", "_nothips", "_public" };
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender), Is.EquivalentTo(expected));

            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var tags = NativeSocketSenders(result.bakeRoot).Single().collisionTags;
            Assert.That(tags.Where(tag => tag.StartsWith("SPS/Socket/")), Is.EquivalentTo(new[] {
                "SPS/Socket/my channel",
                "SPS/Socket/other",
                "SPS/Socket/hand",
                "SPS/Socket/handleft",
                "SPS/Socket/_public", "SPS/Socket/_nothips"
            }));
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender), Is.EquivalentTo(expected));
            socket.tags[0] = "Changed Later";
            Assert.That(tags, Does.Contain("SPS/Socket/my channel"));
            Assert.That(tags, Does.Not.Contain("SPS/Socket/changed later"));
        }

        [TestCase(HumanBodyBones.Hips, false)]
        [TestCase(HumanBodyBones.Head, true)]
        [TestCase(HumanBodyBones.LastBone, true)]
        public void SocketSenderAddsNotHipsOutsideHips(HumanBodyBones bone, bool expected) {
            injector.Set("autoChannelGenerator", new AutoChannels(bone));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            var configurer = injector.GetService<SpsConfigurer>();
            Assert.That(configurer.GetAutoSocketChannels(socket), Does.Not.Contain("_nothips"));
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender).Contains("_nothips"), Is.EqualTo(expected));
            socket.useSharedTag = false;
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender), Does.Not.Contain("_nothips"));
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void PublicSocketRouteUsesLegacyTpsTagAndNeverMatchesGuides(bool global, bool onHips) {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(onHips ? HumanBodyBones.Hips : HumanBodyBones.LeftHand));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useSharedTag = global;
            socket.tags.Add("custom");
            var stop = new GameObject("Stop").transform;
            stop.SetParent(root.transform, false);
            stop.localPosition = Vector3.forward;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var contacts = NativeSocketSenders(result.bakeRoot);
            var entrance = contacts[0].collisionTags;
            var guide = contacts[1].collisionTags;
            Assert.That(entrance.Contains("TPS_Orf_Root"), Is.EqualTo(global));
            Assert.That(entrance.Contains("TPS_Orf_Root_SelfNotOnHips"), Is.EqualTo(global && !onHips));
            Assert.That(entrance.Contains("SPS/Socket/_public"), Is.EqualTo(global));
            Assert.That(entrance.Contains("SPS/Socket/_nothips"), Is.EqualTo(global && !onHips));
            Assert.That(entrance, Does.Not.Contain("SPSN1/Socket"));
            Assert.That(entrance, Does.Not.Contain("SPSN1/Socket_SelfNotOnHips"));
            Assert.That(entrance.Any(t => t.StartsWith("SPS/Socket/00000539")), Is.False);
            Assert.That(guide.Any(t => t.StartsWith("TPS_Orf_Root")), Is.False);
            Assert.That(guide.Any(t => t.StartsWith("SPS/Socket/_public")), Is.False);
            Assert.That(contacts.SelectMany(c => c.collisionTags).Any(t => t.Contains("SCALE_DATA")), Is.False);
            Assert.That(guide.Contains("SPS/SocketPath/_public"), Is.EqualTo(global));
            Assert.That(guide.Contains("SPS/SocketPath/_nothips"), Is.EqualTo(global && !onHips));
            Assert.That(entrance, Does.Contain("SPS/Socket/custom"));
            Assert.That(guide, Does.Contain("SPS/SocketPath/custom"));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false)]
        public void PublicPlugRouteKeepsLegacyTpsTagAndHipPolicy(bool onHips, bool avoidHips) {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(onHips ? HumanBodyBones.Hips : HumanBodyBones.LeftHand));
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.useHipAvoidance = avoidHips;
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var sender = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains("TPS_Pen_Penetrating"));
            Assert.That(sender.collisionTags.Contains("TPS_Pen_Penetrating_SelfNotOnHips"),
                Is.EqualTo(!onHips || !avoidHips));
            Assert.That(sender.collisionTags, Does.Contain("SPS/Plug/_public"));
            Assert.That(sender.collisionTags.Contains("SPS/Plug/_nothips"),
                Is.EqualTo(!onHips || !avoidHips));
            Assert.That(sender.collisionTags, Does.Not.Contain("SPSN1/LegacyPlug/Main"));
            Assert.That(sender.collisionTags, Does.Not.Contain("SPSN1/LegacyPlug/Main_SelfNotOnHips"));
            Assert.That(sender.collisionTags.Any(t => t.StartsWith("SPSN1/Plug/")), Is.False);
            Assert.That(sender.collisionTags.Any(t => t.Contains("SCALE_DATA")), Is.False);
            Assert.That(sender.collisionTags.Where(t => t.StartsWith("SPS/Plug/")), Is.EquivalentTo(
                !onHips || !avoidHips
                    ? new[] { "SPS/Plug/_public", "SPS/Plug/_nothips" }
                    : new[] { "SPS/Plug/_public" }));
            CheckVector(sender.transform.lossyScale, new Vector3(0.03f, 0.0021f, 0.4f));
        }

        [Test]
        public void ReusedRootFitsAllLegacyAndNativeChannelsWithinTagLimit() {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(HumanBodyBones.LeftHand));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.RingOneWay;
            socket.tags.AddRange(new[] { "custom1", "custom2" });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var sender = NativeSocketSenders(result.bakeRoot).Single();
            Assert.That(sender.collisionTags.Count, Is.EqualTo(11));
            foreach (var legacyTag in new[] { SpsTags.LegacySocket, SpsTags.LegacySocket2 }) {
                Assert.That(sender.collisionTags, Does.Contain(legacyTag));
                Assert.That(sender.collisionTags, Does.Contain(legacyTag + "_SelfNotOnHips"));
            }
            foreach (var removedTag in new[] { "SPSLL_Socket_Ring", "SPSLL_Socket_Hole" }) {
                Assert.That(sender.collisionTags, Does.Not.Contain(removedTag));
                Assert.That(sender.collisionTags, Does.Not.Contain(removedTag + "_SelfNotOnHips"));
            }
            var channels = new[] { "custom1", "custom2", "hand", "handleft" }
                .Append("_public").Append("_nothips");
            Assert.That(sender.collisionTags.Where(t => t.StartsWith("SPS/Socket/")),
                Is.EquivalentTo(channels.Select(channel => $"SPS/Socket/{channel}")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlugAndSocketPublishTheSameConfiguredChannels(bool shared) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.useSharedTag = shared;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "  My Channel  ", allowSelf = true, allowOthers = false });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "OTHER", allowSelf = false, allowOthers = true });
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var main = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains(SpsTags.ScaleV1));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useSharedTag = shared;
            socket.tags.AddRange(new[] { "my channel", " other " });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var entrance = NativeSocketSenders(result.bakeRoot).Single();
            var expected = new[] { "my channel", "other" }
                .Concat(shared ? new[] { "_public", "_nothips" } : Array.Empty<string>()).ToArray();
            Assert.That(main.collisionTags.Where(t => t.StartsWith("SPS/Plug/")),
                Is.EquivalentTo(expected.Select(SpsTags.FormatPlug)));
            Assert.That(entrance.collisionTags.Where(t => t.StartsWith("SPS/Socket/")),
                Is.EquivalentTo(expected.Select(SpsTags.FormatSocket)));
            Assert.That(main.collisionTags.Contains(SpsTags.LegacyPlugSphere + "_SelfNotOnHips"), Is.EqualTo(shared));
            var legacyTags = root.GetComponentsInChildren<VRCContactSender>()
                .SelectMany(c => c.collisionTags).Where(t => t.StartsWith("TPS_") || t.StartsWith("SPSLL_")).ToArray();
            if (!shared) Assert.That(legacyTags, Is.EquivalentTo(new[] {
                SpsTags.PlugClose
            }));
        }

        [TestCase("public")]
        [TestCase("nothips")]
        public void UserChannelsAreNotDuplicated(string channel) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "  " + channel.ToUpperInvariant() + "  " });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = channel });
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var main = root.GetComponentsInChildren<VRCContactSender>().Single(c => c.collisionTags.Contains(SpsTags.ScaleV1));
            Assert.That(main.collisionTags, Is.Unique);
            Assert.That(main.collisionTags.Count(t => t == SpsTags.FormatPlug(channel)), Is.EqualTo(1));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.tags.AddRange(new[] { "  " + channel.ToUpperInvariant() + "  ", channel });
            var stop = new GameObject("Stop").transform;
            stop.SetParent(root.transform, false);
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var senders = NativeSocketSenders(result.bakeRoot);
            Assert.That(senders, Has.Length.EqualTo(2));
            Assert.That(senders[0].collisionTags.Count(t => t == SpsTags.FormatSocket(channel)), Is.EqualTo(1));
            Assert.That(senders[1].collisionTags.Count(t => t == SpsTags.FormatSocketPath(channel)), Is.EqualTo(1));
            foreach (var sender in senders) Assert.That(sender.collisionTags, Is.Unique);
        }

        [TestCase("_public", "public")]
        [TestCase("  __NoThips  ", "nothips")]
        [TestCase(" __ \t _NoThips  ", "nothips")]
        [TestCase("___custom", "custom")]
        [TestCase("___", "")]
        public void UserChannelsCannotPublishReservedChannels(string input, string expected) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.useSharedTag = false;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = input });
            var configurer = injector.GetService<SpsConfigurer>();
            var expectedChannels = expected == "" ? Array.Empty<string>() : new[] { expected };
            foreach (SpsConfigurer.ChannelMode mode in Enum.GetValues(typeof(SpsConfigurer.ChannelMode))) {
                Assert.That(configurer.GetPlugChannels(plug, mode), Is.EquivalentTo(expectedChannels));
            }
            var plugResult = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var plugTags = plugResult.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .SelectMany(c => c.collisionTags).ToArray();
            Assert.That(plugTags.Where(tag => tag.StartsWith("TPS_")), Is.EquivalentTo(new[] { SpsTags.PlugClose }));
            Assert.That(plugTags.Where(tag => tag.StartsWith("SPS/Plug/")),
                Is.EquivalentTo(expectedChannels.Select(SpsTags.FormatPlug)));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useSharedTag = false;
            socket.tags.Add(input);
            foreach (SpsConfigurer.ChannelMode mode in Enum.GetValues(typeof(SpsConfigurer.ChannelMode))) {
                Assert.That(configurer.GetSocketChannels(socket, mode), Is.EquivalentTo(expectedChannels));
            }
            var socketResult = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            var socketTags = socketResult.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .SelectMany(c => c.collisionTags).ToArray();
            Assert.That(socketTags.Where(tag => tag.StartsWith("TPS_") || tag.StartsWith("SPSLL_")), Is.Empty);
            Assert.That(socketTags.Where(tag => tag.StartsWith("SPS/Socket/")),
                Is.EquivalentTo(expectedChannels.Select(SpsTags.FormatSocket)));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void PlugChannelsRespectDisabledRulesAndAuthoringLimit(bool allowSelf, bool allowOthers) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.useSharedTag = false;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule {
                tag = "  Custom  ", allowSelf = allowSelf, allowOthers = allowOthers
            });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = " " });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "Outside Authoring Limit" });
            var configurer = injector.GetService<SpsConfigurer>();
            Assert.That(configurer.GetPlugChannels(plug, SpsConfigurer.ChannelMode.Sender), Is.EquivalentTo(
                allowSelf || allowOthers ? new[] { "custom" } : Array.Empty<string>()));
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var main = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains(SpsTags.ScaleV1));
            Assert.That(main.collisionTags.Where(t => t.StartsWith("SPS/Plug/")), Is.EquivalentTo(
                allowSelf || allowOthers ? new[] { "SPS/Plug/custom" } : Array.Empty<string>()));
            Assert.That(root.GetComponentsInChildren<VRCContactSender>(), Has.Length.EqualTo(2));
            var close = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains(SpsTags.PlugClose));
            Assert.That(close.collisionTags, Is.EquivalentTo(new[] { SpsTags.PlugClose }));
            Assert.That(close.radius * MaxScale(close.transform), Is.EqualTo(0.03f).Within(0.000001));
            Assert.That(close.height * MaxScale(close.transform), Is.EqualTo(0.4f).Within(0.000001));
            Assert.That(main.collisionTags.Any(t => t.StartsWith("TPS_")), Is.False);
            Assert.That(main.collisionTags, Does.Contain(SpsTags.ScaleV1));
        }

        [TestCase(HumanBodyBones.Hips, true)]
        [TestCase(HumanBodyBones.Hips, false)]
        [TestCase(HumanBodyBones.Head, true)]
        [TestCase(HumanBodyBones.Head, false)]
        public void ChannelModesRespectPartyRulesAndHipAvoidance(HumanBodyBones bone, bool avoidHips) {
            injector.Set("autoChannelGenerator", new AutoChannels(bone));
            var configurer = injector.GetService<SpsConfigurer>();
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.useHipAvoidance = avoidHips;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "self", allowSelf = true, allowOthers = false });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "others", allowSelf = false, allowOthers = true });
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.tags.Add("custom");

            foreach (SpsConfigurer.ChannelMode mode in Enum.GetValues(typeof(SpsConfigurer.ChannelMode))) {
                var plugExpected = new List<string>();
                if (mode != SpsConfigurer.ChannelMode.ReceiverOthers) plugExpected.Add("self");
                if (mode != SpsConfigurer.ChannelMode.ReceiverSelf) plugExpected.Add("others");
                var socketExpected = new List<string> { "custom" };
                AssertChannels(false);
                AssertChannels(true);

                void AssertChannels(bool shared) {
                    plug.useSharedTag = socket.useSharedTag = shared;
                    var expectedPlug = plugExpected.ToList();
                    var expectedSocket = socketExpected.ToList();
                    if (shared) {
                        expectedPlug.Add(mode == SpsConfigurer.ChannelMode.ReceiverSelf && bone == HumanBodyBones.Hips && avoidHips
                            ? "_nothips" : "_public");
                        expectedSocket.Add(mode == SpsConfigurer.ChannelMode.ReceiverSelf && bone == HumanBodyBones.Hips
                            ? "_nothips" : "_public");
                        expectedSocket.Add(bone == HumanBodyBones.Hips ? "hips" : "head");
                        if (mode == SpsConfigurer.ChannelMode.Sender) {
                            if (bone != HumanBodyBones.Hips || !avoidHips) expectedPlug.Add("_nothips");
                            if (bone != HumanBodyBones.Hips) expectedSocket.Add("_nothips");
                        }
                    }
                    Assert.That(configurer.GetPlugChannels(plug, mode), Is.EquivalentTo(expectedPlug), "Plug " + mode);
                    Assert.That(configurer.GetSocketChannels(socket, mode), Is.EquivalentTo(expectedSocket), "Socket " + mode);
                }
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SelfHipFilteringDoesNotTreatUserPublicAsReserved(bool shared, bool avoidHips) {
            injector.Set("autoChannelGenerator", new AutoChannels(HumanBodyBones.Hips));
            var configurer = injector.GetService<SpsConfigurer>();
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.useSharedTag = shared;
            plug.useHipAvoidance = avoidHips;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "  PUBLIC  " });
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.useSharedTag = shared;
            socket.tags.Add("  PUBLIC  ");
            var plugSelf = new[] { "public" }.Concat(shared
                ? new[] { avoidHips ? "_nothips" : "_public" } : Array.Empty<string>());
            var socketSelf = new[] { "public" }.Concat(shared ? new[] { "_nothips", "hips" } : Array.Empty<string>());
            var others = new[] { "public" }.Concat(shared ? new[] { "_public" } : Array.Empty<string>());
            Assert.That(configurer.GetPlugChannels(plug, SpsConfigurer.ChannelMode.ReceiverSelf), Is.EquivalentTo(plugSelf));
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.ReceiverSelf), Is.EquivalentTo(socketSelf));
            Assert.That(configurer.GetPlugChannels(plug, SpsConfigurer.ChannelMode.ReceiverOthers), Is.EquivalentTo(others));
            Assert.That(configurer.GetSocketChannels(socket, SpsConfigurer.ChannelMode.ReceiverOthers),
                Is.EquivalentTo(others.Concat(shared ? new[] { "hips" } : Array.Empty<string>())));
        }

        [Test]
        public void DisabledSocketDoesNotAdvertiseNativeData() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.None;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            Assert.That(NativeSocketSenders(result.bakeRoot), Is.Empty);
        }

        [Test]
        public void SocketWithNoNativeRoutesDoesNotPublishLegacyTags() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useSharedTag = false;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            Assert.That(NativeSocketSenders(result.bakeRoot), Is.Empty);
            if (!BuildTargetUtils.IsDesktop()) return;
            var legacy = result.senders.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Single();
            Assert.That(legacy.collisionTags, Is.Empty);
            Assert.That(legacy.radius, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EnableAnimationsReachPlugRendererAndBakeRoot(bool componentBinding, bool skinned) {
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector.Set("avatarObject", root.asVf());
            injector.Set("componentObject", null);
            injector.Set(typeof(VRCAvatarDescriptor), avatar);
            injector.Set(typeof(GlobalsService), null);
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            var rendererObject = GameObjects.Create("Renderer", root);
            Renderer renderer = skinned
                ? (Renderer)rendererObject.AddComponent<SkinnedMeshRenderer>()
                : rendererObject.AddComponent<MeshRenderer>();
            var bakeRoot = GameObjects.Create("Baked", root);
            var sourceCurve = AnimationCurve.Linear(0, 0, 1, 1);
            var clip = VFClip.Create("SPS Enable Test");
            clip.SetCurve(componentBinding ? (UnityEngine.Object)plug : renderer,
                componentBinding ? "spsAnimatedEnabled" : "material._TPS_AnimatedToggle", sourceCurve);
            injector.GetService<AllClipsService>().AddAdditionalManagedClip(clip);
            var service = injector.GetService<BakeHapticPlugsService>();
            var rewrites = (List<BakeHapticPlugsService.SpsRewriteToDo>)typeof(BakeHapticPlugsService)
                .GetField("spsRewritesToDo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(service);
            rewrites.Add(new BakeHapticPlugsService.SpsRewriteToDo {
                plugObject = root,
                skin = renderer,
                bakeRoot = bakeRoot,
                spsBlendshapes = Array.Empty<string>(),
                configureMaterial = (_, material) => material
            });
            service.ApplySpsRewrites();
            var curves = clip.GetFloatCurves();
            var enabledCurve = curves.Single(pair => pair.Item1.Targets(rendererObject)
                && pair.Item1.type == renderer.GetType() && pair.Item1.propertyName == "material._SPS_Enabled").Item2;
            var activeCurve = curves.Single(pair => pair.Item1.Targets(bakeRoot)
                && pair.Item1.propertyName == "m_IsActive").Item2;
            foreach (var time in new[] { 0f, 0.5f, 1f }) {
                Assert.That(enabledCurve.Evaluate(time), Is.EqualTo(sourceCurve.Evaluate(time)));
                Assert.That(activeCurve.Evaluate(time), Is.EqualTo(sourceCurve.Evaluate(time)));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeformingPlugKeepsItsBakeWithoutCreatingMarkers(bool withMaterial) {
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(-0.015f, 0, 0), new Vector3(0.015f, 0, 0),
                    new Vector3(-0.015f, 0, 0.2f), new Vector3(0.015f, 0, 0.2f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }
            };
            mesh.RecalculateNormals();
            Material originalMaterial = null;
            try {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                if (withMaterial) originalMaterial = new Material(Shader.Find("Unlit/Color"));
                renderer.sharedMaterials = withMaterial ? new[] { originalMaterial } : Array.Empty<Material>();
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.spsAutorig = false;
                plug.configureTpsMesh.Add(renderer);
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.03f;
                var result = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, deferMaterialConfig: !withMaterial);
                Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.EquivalentTo(new[] { renderer }));
                Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Animator>(), Is.Empty);
                if (!withMaterial) Assert.That(renderer.sharedMaterials, Is.Empty);
                if (BuildTargetUtils.IsDesktop()) {
                    if (withMaterial) {
                        Assert.That(renderer.sharedMaterial, Is.Not.SameAs(originalMaterial));
                        Assert.That(renderer.sharedMaterial.GetTexture("_SPS_Bake"), Is.Not.Null);
                        Assert.That(renderer.sharedMaterial.GetFloat("_SPS_BakedLength"), Is.EqualTo(0.4f));
                        Assert.That(renderer.sharedMaterial.HasProperty("_SPS_IdLow"), Is.False);
                    }
                    var sender = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                        .Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugSphere));
                    CheckVector(sender.transform.lossyScale, new Vector3(0.03f, 0.0021f, 0.4f));
                }
            } finally {
                if (originalMaterial != null) Object.DestroyImmediate(originalMaterial);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void LongPathsAdvertiseBoundedPrefixWithoutChangingAuthoredStops() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            for (var i = 0; i < 7; i++) {
                var stop = new GameObject("Stop " + i).transform;
                stop.SetParent(root.transform, false);
                stop.localPosition = Vector3.forward * (i + 1);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            }
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            if (!BuildTargetUtils.IsDesktop()) return;
            Assert.That(socket.guidedPathStops.All(stop => stop.transform != null), Is.True);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            var contacts = NativeSocketSenders(result.bakeRoot);
            Assert.That(contacts.Length, Is.EqualTo(SpsTags.MaxUserPathStops + 1));
            CheckNode(contacts.Last(), 1, SpsTags.MaxUserPathStops, Flags.RingOneWay);
        }

        [Test]
        public void LegacyOverridesDoNotReplaceNativePoseOrType() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Ring;
            socket.overrideLegacySocketType = socket.overrideLegacyOffset = true;
            socket.legacySocketType = VRCFuryHapticSocket.LegacySocketType.Hole;
            socket.legacyOffsetLocal = new Vector3(0.1f, 0.2f, 0.3f);
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            if (!BuildTargetUtils.IsDesktop()) return;
            var native = NativeSocketSenders(result.bakeRoot).Single();
            CheckNode(native, 1, 0, Flags.Ring);
            CheckVector(native.transform.position, result.bakeRoot.worldPosition);
            Assert.That(result.lights.GetComponentsInSelfAndChildren<Light>().Select(l => l.range),
                Does.Contain(0.4106f));
        }

        [Test]
        public void StaticNonuniformStopScaleDoesNotCorruptPayload() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            var stop = new GameObject("Nonuniform stop").transform;
            stop.SetParent(root.transform, false);
            stop.localPosition = Vector3.forward;
            stop.localRotation = Quaternion.Euler(10, 20, 30);
            stop.localScale = new Vector3(1, 2, 3);
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            if (!BuildTargetUtils.IsDesktop()) return;
            var native = NativeSocketSenders(result.bakeRoot).Last();
            CheckNode(native, 1, 1, Flags.Hole);
            Assert.That(native.transform.lossyScale.x, Is.EqualTo(129 | (1 << 14)).Within(0.01));
            CheckVector(native.transform.forward, stop.forward);
            root.transform.localScale *= 2;
            CheckNode(native, 1, 1, Flags.Hole, 2);
            Assert.That(native.transform.lossyScale.x, Is.EqualTo((129 | (1 << 14)) * 2).Within(0.01));
        }

        [Test]
        public void AutomaticLegacyUpgradePreservesLightsAndAdvertisesNativePose() {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.None;
            socket.fromSpsForAll = true;
            var main = new GameObject("Existing DPS Root").AddComponent<Light>();
            main.transform.SetParent(root.transform, false);
            main.color = Color.black;
            main.range = 0.41f;
            var front = new GameObject("Existing DPS Front").AddComponent<Light>();
            front.transform.SetParent(root.transform, false);
            front.transform.localPosition = Vector3.forward * 0.005f;
            front.color = Color.black;
            front.range = 0.45f;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            Assert.That(main == null || front == null, Is.False);
            Assert.That(main.range, Is.EqualTo(0.41f));
            Assert.That(front.range, Is.EqualTo(0.45f));
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            if (!BuildTargetUtils.IsDesktop()) return;
            var native = NativeSocketSenders(result.bakeRoot).Single();
            CheckNode(native, 1, 0, Flags.Hole);
            CheckVector(native.transform.position, main.transform.position);
            CheckVector(native.transform.forward, (front.transform.position - main.transform.position).normalized);
        }

        [TestCase(0f, 0.03f)]
        [TestCase(float.NaN, 0.03f)]
        [TestCase(float.PositiveInfinity, 0.03f)]
        [TestCase(0.4f, 0f)]
        [TestCase(0.4f, float.NaN)]
        [TestCase(0.4f, float.PositiveInfinity)]
        public void EncodedPlugRequiresFinitePositiveDimensions(float length, float radius) {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HapticUtils.EncodePlugScale(length, radius));
        }

        [TestCase(0.03f, 0.03f)]
        [TestCase(0.4f, 0.4f)]
        [TestCase(0.5f, 0.4f)]
        public void EncodedPlugClampsRadiusToLength(float radius, float expectedRadius) {
            Assert.That(HapticUtils.EncodePlugScale(0.4f, radius),
                Is.EqualTo(new Vector3(expectedRadius, HapticUtils.ScaleVersion, 0.4f)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SenderFactoryAcceptsWorldScaleWithWorldOrLocalShapeUnits(bool useWorldUnits) {
            var sender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest {
                obj = root,
                objName = "Scaled Capsule",
                pos = new Vector3(0.02f, 0.04f, 0.08f),
                radius = 0.03f,
                height = 0.3f,
                rotation = Quaternion.Euler(90, 0, 0),
                worldScale = new Vector3(0.2f, 0.4f, 0.8f),
                useWorldUnits = useWorldUnits,
                tags = new[] { "Finger", SpsTags.FormatPlug("_public") }
            });
            if (!BuildTargetUtils.IsDesktop()) {
                Assert.That(sender, Is.Null);
                return;
            }
            var contact = sender.GetComponent<VRCContactSender>();
            Assert.That(contact.collisionTags, Is.EquivalentTo(new[] { "Finger", SpsTags.FormatPlug("_public") }));
            CheckVector(sender.worldScale, new Vector3(0.2f, 0.4f, 0.8f));
            CheckVector(Vector3.Scale(contact.position, sender.worldScale),
                useWorldUnits ? new Vector3(0.02f, 0.04f, 0.08f) : new Vector3(0.004f, 0.016f, 0.064f));
            Assert.That(contact.radius * MaxScale(sender), Is.EqualTo(useWorldUnits ? 0.03f : 0.024f).Within(0.000001));
            Assert.That(contact.height * MaxScale(sender), Is.EqualTo(useWorldUnits ? 0.3f : 0.24f).Within(0.000001));
        }

        private VRCContactSender ConfigureSocket(VRCFuryHapticSocketBaker service, VFGameObject parent,
            VRCFuryHapticSocket socket, bool isOnHips = false) {
            var sender = GameObjects.Create("Root", parent);
            var contact = sender.AddComponent<VRCContactSender>();
            contact.collisionTags.Add(SpsTags.LegacySocket2);
            var channels = injector.GetService<SpsConfigurer>().GetSocketChannels(socket, SpsConfigurer.ChannelMode.Sender);
            if (isOnHips) channels.Remove("_nothips");
            service.ConfigureSpsSenders(socket, VRCFuryHapticSocket.AddLight.Hole,
                channels, sender);
            return contact;
        }

        private static VRCContactSender[] NativeSocketSenders(VFGameObject parent) =>
            parent.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Where(c => c.collisionTags.Any(t => t == SpsTags.FormatSocket("_public")
                    || t.StartsWith("SPS/Socket/") || t.StartsWith("SPS/SocketPath/")))
                .ToArray();

        private class AutoChannels : SpsAutoChannelGenerator {
            private readonly HumanBodyBones bone;
            public AutoChannels(HumanBodyBones bone) => this.bone = bone;
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => bone;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

        private static float MaxScale(Transform transform) {
            var scale = transform.lossyScale;
            return Mathf.Max(scale.x, scale.y, scale.z);
        }

        private static void CheckVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Mathf.Max(0.0001f, expected.magnitude * 0.000001f)));

        private static void CheckNode(VRCContactSender sender, int group, int pathIndex, Flags flags, float inheritedScale = 1, bool radiusOffset = false) {
            var scale = sender.transform.lossyScale / inheritedScale;
            var packed = Mathf.RoundToInt(scale.x / scale.z);
            Assert.That(packed & 127, Is.EqualTo(group));
            Assert.That((packed >> 7) & 7, Is.EqualTo(pathIndex));
            Assert.That(scale.y / scale.z, Is.EqualTo(0.0021f).Within(0.0000001));
            var expectedFlags = flags == Flags.Hole
                ? new[] { SpsTags.SocketCollapse }
                : flags == Flags.Ring
                    ? new[] { SpsTags.SocketTwoway }
                    : Array.Empty<string>();
            var expectedTags = expectedFlags.Append("SPS/Flag/ScaleV1");
            if (radiusOffset) expectedTags = expectedTags.Append("SPS/Flag/RadiusOffset");
            Assert.That(sender.collisionTags.Where(tag => tag.Contains("/Flag/")), Is.EquivalentTo(expectedTags));
            var expectedBits = flags == Flags.Hole ? 1 << 14 : flags == Flags.Ring ? 1 << 13 : 0;
            Assert.That(packed & 0x7C00, Is.EqualTo(expectedBits | (radiusOffset ? 1 << 12 : 0)));
            Assert.That(NativeSocketSenders(sender.owner()), Does.Contain(sender));
            Assert.That(sender.collisionTags.Count, Is.LessThanOrEqualTo(16));
            Assert.That(sender.radius, Is.Zero);
        }
    }
}
