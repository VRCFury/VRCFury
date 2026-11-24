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

    internal class SpsConfigurerTest : SpsSenderBakeTestBase {
        [Test]
        public void SelfChannelIdIsSharedAcrossComponentsAndChannelSetsButNotBuilds() {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            var channels = injector.GetService<SpsChannelsService>();
            var settings = new VF.Model.SpsChannelSettings {
                useSharedTag = false,
                channels = new List<VF.Model.SpsChannelSettings.Channel> {
                    new() { id = "first" },
                    new() { id = "second" }
                }
            };
            var ids = new HashSet<string>();
            foreach (var component in new UnityEngine.Component[] { plug, socket }) {
                var data = channels.Get(component);
                var custom = data.Request(settings);
                Assert.That(data.Request(settings), Is.SameAs(custom));
                Assert.That(custom.others, Is.EquivalentTo(new[] { "first", "second" }));
                foreach (var set in new[] { data.Default, custom }) {
                    Assert.That(set.self, Is.Not.Empty);
                    foreach (var tag in set.self) {
                        Assert.That(tag, Does.Match(@"^.+/self/[0-9]+$"));
                        Assert.That(uint.TryParse(tag.Substring(tag.LastIndexOf('/') + 1), out _), Is.True);
                        ids.Add(tag.Substring(tag.LastIndexOf('/') + 1));
                        Assert.That(set.sender, Does.Contain(tag));
                    }
                }
            }
            Assert.That(ids, Has.Count.EqualTo(1));
            var otherBuild = new VRCFuryInjector();
            otherBuild.ImportScan(typeof(VFServiceAttribute));
            Assert.That(
                otherBuild.GetService<SpsConfigurer>().selfTagSuffix,
                Is.Not.EqualTo(injector.GetService<SpsConfigurer>().selfTagSuffix)
            );
        }

        [TestCase(HumanBodyBones.Hips, false)]
        [TestCase(HumanBodyBones.Head, true)]
        [TestCase(HumanBodyBones.LastBone, true)]
        public void SocketAutoChannelsAddSelfOnlyNotHipsOutsideHips(HumanBodyBones bone, bool expected) {
            injector.Set("autoChannelGenerator", new AutoChannels(bone));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            var configurer = injector.GetService<SpsConfigurer>();
            Assert.That(configurer.GetAutoSocketChannels(socket.owner(), true).Contains("nothips"), Is.EqualTo(expected));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Merged, socket.channelSettings).Contains(SelfTag("nothips")), Is.EqualTo(expected));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Others, socket.channelSettings), Does.Not.Contain("nothips"));
            socket.channelSettings.useSharedTag = false;
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Merged, socket.channelSettings), Does.Not.Contain(SelfTag("nothips")));
        }

        [Test]
        public void PublicHipAvoidanceMatchesInBothDirections(
            [Values] bool plugOnHips, [Values] bool socketOnHips, [Values] bool avoidHips) {
            var bones = new AutoChannels(HumanBodyBones.Hips);
            injector.Set("autoChannelGenerator", bones);
            var configurer = injector.GetService<SpsConfigurer>();
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            plug.channelSettings.useHipAvoidance = avoidHips;
            bones.bone = plugOnHips ? HumanBodyBones.Hips : HumanBodyBones.Head;
            var plugSelf = configurer.GetChannels(plug.owner(), true, Party.Self, plug.channelSettings);
            var plugOthers = configurer.GetChannels(plug.owner(), true, Party.Others, plug.channelSettings);
            var plugSender = configurer.GetChannels(plug.owner(), true, Party.Merged, plug.channelSettings);
            Assert.That(plugSender, Is.EquivalentTo(plugSelf.Union(plugOthers)));
            bones.bone = socketOnHips ? HumanBodyBones.Hips : HumanBodyBones.Head;
            var socketSelf = configurer.GetChannels(socket.owner(), false, Party.Self, socket.channelSettings);
            var socketOthers = configurer.GetChannels(socket.owner(), false, Party.Others, socket.channelSettings);
            var socketSender = configurer.GetChannels(socket.owner(), false, Party.Merged, socket.channelSettings);
            Assert.That(socketSender, Is.EquivalentTo(socketSelf.Union(socketOthers)));
            var matchesSelf = !(plugOnHips && socketOnHips && avoidHips);
            Assert.That(plugSender.Overlaps(socketSelf), Is.EqualTo(matchesSelf), "Plug sender to self socket");
            Assert.That(socketSender.Overlaps(plugSelf), Is.EqualTo(matchesSelf), "Socket sender to self plug");
            Assert.That(plugSender.Overlaps(socketOthers), Is.True, "Other avatars must be unaffected");
            Assert.That(socketSender.Overlaps(plugOthers), Is.True, "Other avatars must be unaffected");
        }

        [TestCase(HumanBodyBones.Hips, true)]
        [TestCase(HumanBodyBones.Hips, false)]
        [TestCase(HumanBodyBones.Head, true)]
        [TestCase(HumanBodyBones.Head, false)]
        public void ChannelsRespectPartyRulesAndHipAvoidance(HumanBodyBones bone, bool avoidHips) {
            injector.Set("autoChannelGenerator", new AutoChannels(bone));
            var configurer = injector.GetService<SpsConfigurer>();
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.channelSettings.useHipAvoidance = avoidHips;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "self", allowSelf = true, allowOthers = false });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "others", allowSelf = false, allowOthers = true });
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "custom" });

            foreach (Party party in Enum.GetValues(typeof(Party))) {
                var plugExpected = new List<string>();
                if (party != Party.Others) plugExpected.Add(SelfTag("self"));
                if (party != Party.Self) plugExpected.Add("others");
                var socketExpected = party == Party.Merged ? new List<string> { "custom", SelfTag("custom") }
                    : new List<string> { party == Party.Self ? SelfTag("custom") : "custom" };
                AssertChannels(false);
                AssertChannels(true);

                void AssertChannels(bool shared) {
                    plug.channelSettings.useSharedTag = socket.channelSettings.useSharedTag = shared;
                    var expectedPlug = plugExpected.ToList();
                    var expectedSocket = socketExpected.ToList();
                    if (shared) {
                        var auto = bone == HumanBodyBones.Hips ? "hips" : "head";
                        if (party != Party.Self) {
                            expectedPlug.Add("_public");
                            expectedSocket.Add("_public");
                            expectedSocket.Add(auto);
                        }
                        if (party != Party.Others) {
                            expectedPlug.Add(SelfTag(bone == HumanBodyBones.Hips && avoidHips ? "nothips" : "_public"));
                            expectedSocket.Add(SelfTag("_public"));
                            expectedSocket.Add(SelfTag(auto));
                            if (bone != HumanBodyBones.Hips) expectedSocket.Add(SelfTag("nothips"));
                        }
                    }
                    Assert.That(configurer.GetChannels(plug.owner(), true, party, plug.channelSettings), Is.EquivalentTo(expectedPlug), "Plug " + party);
                    Assert.That(configurer.GetChannels(socket.owner(), false, party, socket.channelSettings), Is.EquivalentTo(expectedSocket), "Socket " + party);
                    Assert.That(configurer.GetChannels(plug.owner(), true, party, new VF.Model.SpsChannelSettings {
                        useSharedTag = shared, useHipAvoidance = avoidHips, channels = plug.channelSettings.channels
                    }), Is.EquivalentTo(expectedPlug), "Plug action " + party);
                    Assert.That(configurer.GetChannels(socket.owner(), false, party, new VF.Model.SpsChannelSettings {
                        useSharedTag = shared, channels = socket.channelSettings.channels
                    }), Is.EquivalentTo(expectedSocket), "Socket action " + party);
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
            plug.channelSettings.useSharedTag = shared;
            plug.channelSettings.useHipAvoidance = avoidHips;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "  PUBLIC  " });
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = shared;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "  PUBLIC  " });
            var plugSelf = new[] { "public" }.Concat(shared
                ? new[] { avoidHips ? "nothips" : "_public" } : Array.Empty<string>());
            var socketSelf = new[] { "public" }.Concat(shared ? new[] { "_public", "hips" } : Array.Empty<string>());
            var others = new[] { "public" }.Concat(shared ? new[] { "_public" } : Array.Empty<string>());
            Assert.That(configurer.GetChannels(plug.owner(), true, Party.Self, plug.channelSettings), Is.EquivalentTo(plugSelf.Select(SelfTag)));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Self, socket.channelSettings), Is.EquivalentTo(socketSelf.Select(SelfTag)));
            Assert.That(configurer.GetChannels(plug.owner(), true, Party.Others, plug.channelSettings), Is.EquivalentTo(others));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Others, socket.channelSettings),
                Is.EquivalentTo(others.Concat(shared ? new[] { "hips" } : Array.Empty<string>())));
        }
    }
}
