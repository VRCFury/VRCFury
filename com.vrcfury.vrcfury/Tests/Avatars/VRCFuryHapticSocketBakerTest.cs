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

    internal class VRCFuryHapticSocketBakerTest : SpsSenderBakeTestBase {
        [TestCase(2, 1, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(-1, 1, 1)]
        public void StandaloneSocketBakeStillRejectsInvalidScale(float x, float y, float z) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var obj = GameObjects.Create("World socket", root);
            obj.localScale = new Vector3(x, y, z);
            var socket = obj.AddComponent<VRCFuryHapticSocket>();
            Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket));
            Assert.That(obj.childCount, Is.Zero, "Validation must fail before creating a placeholder or baked objects");
        }

        [TestCase(false, 0f)]
        [TestCase(false, .025f)]
        [TestCase(true, 0f)]
        [TestCase(true, .025f)]
        public void SocketBakesIndependentRadiusCapsInNodeLocalUnits(bool enabled, float cap) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            Assert.That(socket.maxRadiusOffset, Is.Zero);
            socket.addLight = Flags.Ring;
            socket.useRadiusOffset = enabled;
            socket.maxRadiusOffset = cap;
            var stop = GameObjects.Create("Guide", root);
            stop.localPosition = Vector3.forward;
            stop.localScale = Vector3.one * 3;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                transform = stop,
                useRadiusOffset = true,
                maxRadiusOffset = cap * 2
            });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var senders = NativeSocketSenders(result.bakeRoot).OrderBy(s => s.transform.lossyScale.x).ToArray();
            Assert.That(result.mainSender.worldScale.y, Is.EqualTo(enabled && cap > 0 ? 2 + cap * 2 : 1f).Within(.000001f));
            Assert.That(senders, Has.Length.EqualTo(2));
            Assert.That(senders.Single(s => s.transform != (Transform)result.mainSender).transform.lossyScale.y,
                Is.EqualTo(cap > 0 ? 2 + cap * 2 * stop.worldScale.x : 1).Within(.00001f));
        }

        [TestCase((int)VRCFuryHapticSocket.AddLight.Hole, (int)Flags.Hole)]
        [TestCase((int)VRCFuryHapticSocket.AddLight.Ring, (int)Flags.Ring)]
        [TestCase((int)VRCFuryHapticSocket.AddLight.RingOneWay, (int)Flags.RingOneWay)]
        public void SocketBakerKeepsLightsAndLegacyContactsWithoutMarkers(int typeValue, int flagValue) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var type = (VRCFuryHapticSocket.AddLight)typeValue;
            var flags = (Flags)flagValue;
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = type;
            socket.useRadiusOffset = true;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);

            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Animator>(), Is.Empty);
            var lights = result.legacyLights.GetComponentsInSelfAndChildren<Light>();
            Assert.That(lights.Select(l => l.range), Is.EquivalentTo(new[] {
                type == VRCFuryHapticSocket.AddLight.Hole ? 0.4104f : 0.4204f, 0.4504f
            }));
            var legacy = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>();
            Assert.That(legacy.Length, Is.EqualTo(1));
            CheckVector(legacy.Single(c => c.collisionTags.Contains(SpsTags.LegacySocket2)).transform.position,
                result.bakeRoot.worldPosition);
            Assert.That(legacy.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("SPSN1/Legacy")), Is.False);
            var contact = NativeSocketSenders(result.bakeRoot).Single();
            CheckNode(contact, 1, 0, flags, radiusOffset: true, animatorHole: flags == Flags.Hole);
            Assert.That(contact, Is.SameAs(legacy.Single(c => c.collisionTags.Contains(SpsTags.LegacySocket2))));
            Assert.That(contact.transform.parent.asVf(), Is.EqualTo(result.worldSpace));
            CheckVector(result.worldSpace.worldScale, Vector3.one);
            Assert.That(result.worldSpace.GetComponent<UnityEngine.Animations.ScaleConstraint>(), Is.Not.Null);
            Assert.That(result.bakeRoot.Children().Select(child => child.name), Is.EquivalentTo(new[] { "WorldSpace", "LegacyLights", "ReceiverSpace" }));
            var typeBits = flags == Flags.Hole ? 1 << 14 : flags == Flags.Ring ? 1 << 13 : 0;
            Assert.That(contact.transform.lossyScale.x, Is.EqualTo(1 | typeBits | (1 << 12)).Within(0.01));
            Assert.That(legacy.All(c => c.radius == 0), Is.True);
            Assert.That(legacy.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("TPS_Orf_Norm")), Is.False);
            CheckVector(contact.transform.position, result.bakeRoot.worldPosition);
            CheckVector(contact.transform.forward, result.bakeRoot.TransformDirection(Vector3.forward));
            Assert.That(Vector3.Distance(contact.transform.position, lights.First().transform.position), Is.GreaterThan(0.05));
            root.transform.localScale *= 3;
            CheckNode(contact, 1, 0, flags, 3, radiusOffset: true, animatorHole: flags == Flags.Hole);
            result.bakeRoot.active = false;
            Assert.That(contact.gameObject.activeInHierarchy, Is.False);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GuidedRecordsKeepOrderShrinkTerminalAndStopTransforms(bool collapseAtEntrance) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Ring;
            socket.useLights = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "  CuStOm  " });
            for (var i = 0; i < 3; i++) {
                var stop = new GameObject("Guide " + i).transform;
                stop.SetParent(root.transform, false);
                stop.localPosition = new Vector3(i + 1, i * 0.1f, 0.2f);
                stop.localRotation = Quaternion.Euler(i * 20, 10, 30);
                stop.localScale = Vector3.one * (i + 1);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                    transform = stop,
                    shrink = i == (collapseAtEntrance ? 0 : 2)
                });
            }
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            if (!BuildTargetUtils.IsDesktop()) return;
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            var contacts = NativeSocketSenders(result.bakeRoot);
            Assert.That(contacts.Length, Is.EqualTo(4));
            CheckNode(contacts[0], 1, 0, collapseAtEntrance ? Flags.Hole : Flags.RingOneWay, animatorHole: true);
            CheckNode(contacts[1], 1, 1, Flags.RingOneWay);
            CheckNode(contacts[2], 1, 2, collapseAtEntrance ? Flags.RingOneWay : Flags.Hole);
            CheckNode(contacts[3], 1, 3, Flags.RingOneWay);
            for (var i = 1; i < contacts.Length; i++) {
                var target = socket.guidedPathStops[i - 1].transform;
                var pathRoot = target.asVf().Children().Single(child => child.name == "BakedSpsPath");
                CheckVector(contacts[i].transform.position, target.position);
                CheckVector(contacts[i].transform.forward, target.forward);
                Assert.That(contacts[i].transform.lossyScale.x, Is.EqualTo(1 | (i << 7) | (i == 2 && !collapseAtEntrance ? 1 << 14 : 0)).Within(0.01));
                Assert.That(contacts[i].radius, Is.Zero);
                Assert.That(contacts[i].transform.parent.asVf(), Is.EqualTo(result.worldSpace));
                var constraints = VFConstraint.GetConstraintsInSelfAndChildren(new[] { result.bakeRoot })
                    .Where(c => c.GetSources().Single() == pathRoot).ToArray();
                Assert.That(constraints.Length, Is.EqualTo(1));
                Assert.That(constraints.All(c => c.GetSources().Single() == pathRoot), Is.True);
                Assert.That(contacts[i].collisionTags, Does.Contain("SPS/SocketPath/custom"));
                Assert.That(contacts[i].collisionTags, Does.Contain(SelfTag("SPS/SocketPath/nothips")));
            }
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>().Length, Is.EqualTo(4));
            Assert.That(VFConstraint.GetConstraintsInSelfAndChildren(new[] { result.bakeRoot }).Count(), Is.EqualTo(5));
            result.bakeRoot.active = false;
            Assert.That(contacts.All(c => !c.gameObject.activeInHierarchy), Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void GuidedNodeRadiusOffsetIsIndependentOfEntrance(bool entranceOffset, bool nodeOffset) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.useRadiusOffset = entranceOffset;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                transform = GameObjects.Create("Guide", root),
                useRadiusOffset = nodeOffset
            });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var senders = NativeSocketSenders(result.bakeRoot);
            Assert.That(senders, Has.Length.EqualTo(2));
            CheckNode(senders[0], 1, 0, Flags.RingOneWay, radiusOffset: entranceOffset);
            CheckNode(senders[1], 1, 1, Flags.RingOneWay, radiusOffset: nodeOffset);
        }

        [TestCase(HumanBodyBones.Hips, false)]
        [TestCase(HumanBodyBones.Hips, true)]
        [TestCase(HumanBodyBones.Head, false)]
        [TestCase(HumanBodyBones.Head, true)]
        [TestCase(HumanBodyBones.LeftHand, false)]
        [TestCase(HumanBodyBones.LeftHand, true)]
        public void AutoOnlyDetectsHoleWhenThereIsNoGuidedPath(HumanBodyBones bone, bool guided) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("autoChannelGenerator", new AutoChannels(bone));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = Flags.Auto;
            if (guided) {
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                    transform = GameObjects.Create("Guide", root)
                });
            }
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var senders = NativeSocketSenders(result.bakeRoot);
            if (guided) {
                Assert.That(senders, Has.Length.EqualTo(2));
                CheckNode(senders[0], 1, 0, Flags.RingOneWay);
                CheckNode(senders[1], 1, 1, Flags.RingOneWay);
            } else {
                var hole = bone != HumanBodyBones.LeftHand;
                CheckNode(senders.Single(), 1, 0, hole ? Flags.Hole : Flags.Ring, animatorHole: hole);
            }
        }

        [Test]
        public void EachBakeGetsAUniqueGroupId() {
            if (!BuildTargetUtils.IsDesktop()) return;
            var service = injector.GetService<VRCFuryHapticSocketBaker>();
            var socketA = root.AddComponent<VRCFuryHapticSocket>();
            var other = new GameObject("Other");
            other.transform.SetParent(root.transform, false);
            var socketB = other.AddComponent<VRCFuryHapticSocket>();
            socketA.addLight = socketB.addLight = VRCFuryHapticSocket.AddLight.Hole;
            var a = service.Bake(socketA);
            var b = service.Bake(socketB);
            var c = service.Bake(socketA);
            var channels = injector.GetService<SpsChannelsService>();
            channels.Apply(channels.Get(socketA));
            channels.Apply(channels.Get(socketB));
            CheckNode(NativeSocketSenders(a.bakeRoot).Single(), 1, 0, Flags.Hole, animatorHole: true);
            CheckNode(NativeSocketSenders(b.bakeRoot).Single(), 2, 0, Flags.Hole, animatorHole: true);
            CheckNode(NativeSocketSenders(c.bakeRoot).Single(), 3, 0, Flags.Hole, animatorHole: true);
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
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var senders = NativeSocketSenders(result.bakeRoot);
            CheckNode(senders.First(), 1, 0, Flags.RingOneWay, animatorHole: true);
            var guide = senders.Last();
            var expected = new Vector3(129 | (1 << 14), 1f, 1);
            CheckVector(guide.transform.lossyScale, expected);
            stop.localScale *= 10;
            CheckVector(guide.transform.lossyScale, expected);
            Assert.That(guide.transform.lossyScale.y, Is.EqualTo(1).Within(0.000001));
            var constraint = VFConstraint.GetConstraintsInSelfAndChildren(new[] { guide.owner() }).Single();
            Assert.That(constraint.GetSources().Single(), Is.EqualTo(stop.asVf().Children().Single(child => child.name == "BakedSpsPath")));
            result.bakeRoot.active = false;
            Assert.That(guide.gameObject.activeInHierarchy, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoutingPreservesSharedOptOutAndNormalization(bool custom) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = false;
            if (custom) socket.channelSettings.channels.AddRange(new[] { "  CuStOm ", "custom" }.Select(tag => new VF.Model.SpsChannelSettings.Channel { id = tag }));
            if (!custom) {
                Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket));
                return;
            }
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(bake.channelContacts);
            var tags = NativeSocketSenders(bake.bakeRoot).SelectMany(s => s.collisionTags)
                .Where(t => t.StartsWith("SPS/Socket/"));
            Assert.That(tags, Is.EquivalentTo(custom ? new[] { "SPS/Socket/custom", SelfTag("SPS/Socket/custom") } : Array.Empty<string>()));
        }

        [Test]
        public void ContactTagsUseConfiguredChannels() {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(HumanBodyBones.LeftHand));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.channels.AddRange(new[] { "  My Channel  ", "Other", "Ignored Third" }.Select(tag => new VF.Model.SpsChannelSettings.Channel { id = tag }));
            var configurer = injector.GetService<SpsConfigurer>();
            var expected = new[] { "my channel", "other", "hand", "handleft", "_public" }
                .SelectMany(c => new[] { c, SelfTag(c) }).Append(SelfTag("nothips")).ToArray();
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Merged, socket.channelSettings), Is.EquivalentTo(expected));

            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var tags = NativeSocketSenders(result.bakeRoot).Single().collisionTags;
            Assert.That(tags.Where(tag => tag.StartsWith("SPS/Socket/")), Is.EquivalentTo(expected.Select(SpsTags.FormatSocket)));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Merged, socket.channelSettings), Is.EquivalentTo(expected));
            socket.channelSettings.channels[0].id = "Changed Later";
            Assert.That(tags, Does.Contain("SPS/Socket/my channel"));
            Assert.That(tags, Does.Not.Contain("SPS/Socket/changed later"));
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
            socket.channelSettings.useSharedTag = global;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "custom" });
            var stop = new GameObject("Stop").transform;
            stop.SetParent(root.transform, false);
            stop.localPosition = Vector3.forward;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var contacts = NativeSocketSenders(result.bakeRoot);
            var entrance = contacts[0].collisionTags;
            var guide = contacts[1].collisionTags;
            Assert.That(entrance.Contains("TPS_Orf_Root"), Is.EqualTo(global));
            Assert.That(entrance.Contains("TPS_Orf_Root_SelfNotOnHips"), Is.False);
            Assert.That(entrance.Contains("SPS/Socket/_public"), Is.EqualTo(global));
            Assert.That(entrance.Contains(SelfTag("SPS/Socket/nothips")), Is.EqualTo(global && !onHips));
            Assert.That(entrance, Does.Not.Contain("SPS/Socket/nothips"));
            Assert.That(entrance, Does.Not.Contain("SPSN1/Socket"));
            Assert.That(entrance, Does.Not.Contain("SPSN1/Socket_SelfNotOnHips"));
            Assert.That(entrance.Any(t => t.StartsWith("SPS/Socket/00000539")), Is.False);
            Assert.That(guide.Any(t => t.StartsWith("TPS_Orf_Root")), Is.False);
            Assert.That(guide.Any(t => t.StartsWith("SPS/Socket/_public")), Is.False);
            Assert.That(contacts.SelectMany(c => c.collisionTags).Any(t => t.Contains("SCALE_DATA")), Is.False);
            Assert.That(guide.Contains("SPS/SocketPath/_public"), Is.EqualTo(global));
            Assert.That(guide.Contains(SelfTag("SPS/SocketPath/nothips")), Is.EqualTo(global && !onHips));
            Assert.That(guide, Does.Not.Contain("SPS/SocketPath/nothips"));
            Assert.That(entrance, Does.Contain("SPS/Socket/custom"));
            Assert.That(guide, Does.Contain("SPS/SocketPath/custom"));
        }

        [Test]
        public void ReusedRootFitsAllLegacyAndNativeChannelsWithinTagLimit() {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(HumanBodyBones.LeftHand));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.RingOneWay;
            socket.channelSettings.channels.AddRange(new[] { "custom1", "custom2" }.Select(tag => new VF.Model.SpsChannelSettings.Channel { id = tag }));
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var sender = NativeSocketSenders(result.bakeRoot).Single();
            Assert.That(sender.collisionTags.Count, Is.EqualTo(13));
            foreach (var legacyTag in new[] { SpsTags.LegacySocket, SpsTags.LegacySocket2 }) {
                Assert.That(sender.collisionTags, Does.Contain(legacyTag));
                Assert.That(sender.collisionTags, Does.Not.Contain(legacyTag + "_SelfNotOnHips"));
            }
            foreach (var removedTag in new[] { "SPSLL_Socket_Ring", "SPSLL_Socket_Hole" }) {
                Assert.That(sender.collisionTags, Does.Not.Contain(removedTag));
                Assert.That(sender.collisionTags, Does.Not.Contain(removedTag + "_SelfNotOnHips"));
            }
            var channels = new[] { "custom1", "custom2", "hand", "handleft", "_public" }
                .SelectMany(c => new[] { c, SelfTag(c) }).Append(SelfTag("nothips"));
            Assert.That(sender.collisionTags.Where(t => t.StartsWith("SPS/Socket/")),
                Is.EquivalentTo(channels.Select(channel => $"SPS/Socket/{channel}")));
        }

        [Test]
        public void DisabledSocketCreatesNoSendersOrLights([Values] bool legacy, [Values] bool tpsDepth) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.None;
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket,
                legacyPlugCompatibility: legacy, tpsDepthAnimationCompatibility: tpsDepth);
            result.plugReceivers.Find(false, false);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>(), Is.Empty);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Light>(), Is.Empty);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>(), Is.Not.Empty);
            Assert.That(result.pathRoots, Is.Empty);
        }

        [Test]
        public void SocketWithNoChannelsRejectsBaking() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.useSharedTag = false;
            Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket));
        }

        [Test]
        public void LongPathsAdvertiseBoundedPrefixWithoutChangingAuthoredStops() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            for (var i = 0; i < 7; i++) {
                var stop = new GameObject("Stop " + i).transform;
                stop.SetParent(root.transform, false);
                stop.localPosition = Vector3.forward * (i + 1);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            }
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            if (!BuildTargetUtils.IsDesktop()) return;
            Assert.That(socket.guidedPathStops.All(stop => stop.transform != null), Is.True);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Renderer>(), Is.Empty);
            var contacts = NativeSocketSenders(result.bakeRoot);
            Assert.That(contacts.Length, Is.EqualTo(SpsTags.MaxUserPathStops + 1));
            CheckNode(contacts.Last(), 1, SpsTags.MaxUserPathStops, Flags.RingOneWay);
        }

        [Test]
        public void LegacyOverridesDoNotReplaceNativePoseOrType() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Ring;
            socket.overrideLegacySocketType = socket.overrideLegacyOffset = true;
            socket.legacySocketType = VRCFuryHapticSocket.LegacySocketType.Hole;
            socket.legacyOffsetLocal = new Vector3(0.1f, 0.2f, 0.3f);
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            if (!BuildTargetUtils.IsDesktop()) return;
            var native = NativeSocketSenders(result.bakeRoot).Single();
            CheckNode(native, 1, 0, Flags.Ring);
            CheckVector(native.transform.position, result.bakeRoot.worldPosition);
            Assert.That(result.legacyLights.GetComponentsInSelfAndChildren<Light>().Select(l => l.range),
                Does.Contain(0.4104f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeparateSocketFramesPreserveTheAuthoredOffsetAndFrontSpacing(bool overrideOffset) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.useRadiusOffset = true;
            socket.overrideLegacyOffset = overrideOffset;
            socket.legacyOffsetLocal = new Vector3(0.1f, 0.2f, 0.3f);
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, tpsDepthAnimationCompatibility: true);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            if (!BuildTargetUtils.IsDesktop()) return;
            var offset = overrideOffset ? socket.legacyOffsetLocal : Vector3.up * VRCFuryHapticSocketEditor.LegacyRadiusOffset;
            var main = result.legacyLights.Children().Single(obj => obj.name == "Light - Legacy Root");
            var front = result.legacyLights.Children().Single(obj => obj.name == "Light - Legacy Front");
            CheckVector(main.worldPosition, result.bakeRoot.worldPosition + result.bakeRoot.worldRotation * (offset * 2));
            CheckVector(front.worldPosition - main.worldPosition, result.bakeRoot.worldRotation * Vector3.forward * 0.01f);
            Assert.That(result.bakeRoot.Children().Select(obj => obj.name), Is.EquivalentTo(new[] {
                "WorldSpace", "LegacyLights", "ReceiverSpace"
            }));
            Assert.That(result.worldSpace.Children().Select(obj => obj.name), Is.EquivalentTo(new[] { "Sender - Root", "Sender - Legacy Front" }));
            Assert.That(result.legacyLights.Children().Select(obj => obj.name), Is.EquivalentTo(new[] { "Light - Legacy Root", "Light - Legacy Front" }));
            Assert.That(result.worldSpace.Children().All(obj => obj.Children().Length == 0), Is.True);
            Assert.That(result.legacyLights.Children().All(obj => obj.Children().Length == 0), Is.True);
            CheckVector(result.legacyLights.localPosition, offset);
            Assert.That(result.bakeRoot.GetComponent<UnityEngine.Animations.ScaleConstraint>(), Is.Null);
            Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Animations.ScaleConstraint>(), Has.Length.EqualTo(3));
        }

        [Test]
        public void StaticNonuniformStopScaleDoesNotCorruptPayload() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            var stop = new GameObject("Nonuniform stop").transform;
            stop.SetParent(root.transform, false);
            stop.localPosition = Vector3.forward;
            stop.localRotation = Quaternion.Euler(10, 20, 30);
            stop.localScale = new Vector3(1, 2, 3);
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            if (!BuildTargetUtils.IsDesktop()) return;
            var native = NativeSocketSenders(result.bakeRoot).Last();
            CheckNode(native, 1, 1, Flags.Hole);
            Assert.That(native.transform.lossyScale.x, Is.EqualTo(129 | (1 << 14)).Within(0.01));
            CheckVector(native.transform.forward, stop.forward);
            root.transform.localScale *= 2;
            CheckNode(native, 1, 1, Flags.Hole, 2);
            Assert.That(native.transform.lossyScale.x, Is.EqualTo((129 | (1 << 14)) * 2).Within(0.01));
        }
    }
}
