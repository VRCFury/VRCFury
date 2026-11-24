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

    internal class SpsChannelsServiceTest : SpsSenderBakeTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void PlugAndSocketPublishConfiguredChannels(bool shared) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.channelSettings.useSharedTag = shared;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "  My Channel  ", allowSelf = true, allowOthers = false });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "OTHER", allowSelf = false, allowOthers = true });
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(injector.GetService<SpsChannelsService>().Get(plug));
            var main = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.name == "Sender - Sphere");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.useSharedTag = shared;
            socket.channelSettings.channels.AddRange(plug.channelSettings.channels.Select(rule => new VF.Model.SpsChannelSettings.Channel {
                id = rule.id, allowSelf = rule.allowSelf, allowOthers = rule.allowOthers
            }));
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var entrance = NativeSocketSenders(result.bakeRoot).Single();
            Assert.That(result.legacyLights != null, Is.EqualTo(shared));
            var expected = new[] { SelfTag("my channel"), "other" }
                .Concat(shared ? new[] { "_public", SelfTag("_public") } : Array.Empty<string>()).ToArray();
            Assert.That(main.collisionTags.Where(t => t.StartsWith("SPS/Plug/")),
                Is.EquivalentTo(expected.Select(SpsTags.FormatPlug)));
            Assert.That(entrance.collisionTags.Where(t => t.StartsWith("SPS/Socket/")),
                Is.EquivalentTo(expected.Concat(shared ? new[] { SelfTag("nothips") } : Array.Empty<string>()).Select(SpsTags.FormatSocket)));
            Assert.That(main.collisionTags.Contains(SpsTags.LegacyPlugSphere + "_SelfNotOnHips"), Is.False);
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
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "  " + channel.ToUpperInvariant() + "  " });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = channel });
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(injector.GetService<SpsChannelsService>().Get(plug));
            var main = root.GetComponentsInChildren<VRCContactSender>().Single(c => c.name == "Sender - Sphere");
            Assert.That(main.collisionTags, Is.Unique);
            Assert.That(main.collisionTags.Count(t => t == SpsTags.FormatPlug(channel)), Is.EqualTo(1));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.channels.AddRange(new[] { "  " + channel.ToUpperInvariant() + "  ", channel }.Select(tag => new VF.Model.SpsChannelSettings.Channel { id = tag }));
            var stop = new GameObject("Stop").transform;
            stop.SetParent(root.transform, false);
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var result = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
            var senders = NativeSocketSenders(result.bakeRoot);
            Assert.That(senders, Has.Length.EqualTo(2));
            Assert.That(senders[0].collisionTags.Count(t => t == SpsTags.FormatSocket(channel)), Is.EqualTo(1));
            Assert.That(senders[1].collisionTags.Count(t => t == SpsTags.FormatSocketPath(channel)), Is.EqualTo(1));
            foreach (var sender in senders) Assert.That(sender.collisionTags, Is.Unique);
        }

        [TestCase("_public", "_public")]
        [TestCase("  __NoThips  ", "__nothips")]
        [TestCase("___custom", "___custom")]
        [TestCase("___", "___")]
        [TestCase("Custom_Channel_", "custom_channel_")]
        public void UserChannelsPreserveUnderscores(string input, string expected) {
            if (!BuildTargetUtils.IsDesktop()) return;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.channelSettings.useSharedTag = false;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = input });
            var configurer = injector.GetService<SpsConfigurer>();
            foreach (Party party in Enum.GetValues(typeof(Party))) {
                Assert.That(configurer.GetChannels(plug.owner(), true, party, plug.channelSettings), Does.Contain(party == Party.Self ? SelfTag(expected) : expected));
            }
            var plugBake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(plugBake.channelContacts);
            Assert.That(plugBake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .SelectMany(s => s.collisionTags), Does.Contain(SpsTags.FormatPlug(expected)));
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = input });
            foreach (Party party in Enum.GetValues(typeof(Party))) {
                Assert.That(configurer.GetChannels(socket.owner(), false, party, socket.channelSettings), Does.Contain(party == Party.Self ? SelfTag(expected) : expected));
            }
            var socketBake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket);
            injector.GetService<SpsChannelsService>().Apply(socketBake.channelContacts);
            Assert.That(socketBake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .SelectMany(s => s.collisionTags), Does.Contain(SpsTags.FormatSocket(expected)));
            var action = new ChangeSpsChannelsAction {
                channelSettings = new VF.Model.SpsChannelSettings {
                    useSharedTag = false, channels = { new VF.Model.SpsChannelSettings.Channel { id = input } }
                }
            };
            Assert.That(configurer.GetChannels(plug.owner(), true, Party.Merged, action.channelSettings), Does.Contain(expected));
            Assert.That(configurer.GetChannels(socket.owner(), false, Party.Merged, action.channelSettings), Does.Contain(expected));
            Assert.That(VRCFuryHapticPlugEditor.SanitizeSpsChannel(input), Is.EqualTo(expected));
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
            plug.channelSettings.useSharedTag = false;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel {
                id = "  Custom  ", allowSelf = allowSelf, allowOthers = allowOthers
            });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = " " });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "Outside Authoring Limit" });
            var configurer = injector.GetService<SpsConfigurer>();
            var expected = (allowSelf ? new[] { SelfTag("custom") } : Array.Empty<string>())
                .Concat(allowOthers ? new[] { "custom" } : Array.Empty<string>()).ToArray();
            Assert.That(configurer.GetChannels(plug.owner(), true, Party.Merged, plug.channelSettings), Is.EquivalentTo(expected));
            if (!allowSelf && !allowOthers) {
                Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug));
                return;
            }
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(injector.GetService<SpsChannelsService>().Get(plug));
            var main = root.GetComponentsInChildren<VRCContactSender>()
                .SingleOrDefault(c => c.name == "Sender - Sphere");
            Assert.That(main == null ? Array.Empty<string>() : main.collisionTags.Where(t => t.StartsWith("SPS/Plug/")), Is.EquivalentTo(
                expected.Select(SpsTags.FormatPlug)));
            Assert.That(root.GetComponentsInChildren<VRCContactSender>(), Has.Length.EqualTo(allowSelf || allowOthers ? 2 : 1));
            var close = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains(SpsTags.PlugClose));
            Assert.That(close.collisionTags, Is.EquivalentTo(new[] { SpsTags.PlugClose }));
            Assert.That(close.radius * MaxScale(close.transform), Is.EqualTo(0.03f).Within(0.000001));
            Assert.That(close.height * MaxScale(close.transform), Is.EqualTo(0.4f).Within(0.000001));
            if (main != null) {
                Assert.That(main.collisionTags.Any(t => t.StartsWith("TPS_")), Is.False);
                Assert.That(main.collisionTags.Any(tag => tag.StartsWith("SPS/Flag/")), Is.False);
            }
        }
    }
}

namespace VF.Tests {
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Actions;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.SDK3.Dynamics.PhysBone.Components;
    using Object = UnityEngine.Object;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

    internal class SpsChannelsFinalizationTest : SpsChannelBuildTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void CallbacksSeeEveryDistinctSetOnceAfterLateRequests(bool publicRequested) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Callbacks", root);
            var bake = BakePlug(Plug(obj, false));
            var data = bake.channelContacts;
            var seen = new List<SpsChannelsService.ChannelSet>();
            var owners = new List<VFGameObject>();
            var publicCalls = 0;
            var allCalls = 0;
            data.OnAllSetsAvailable(sets => {
                foreach (var set in sets) {
                    seen.Add(set);
                    var owner = GameObjects.Create("Owned handler", bake.bakeRoot);
                    owners.Add(owner);
                    set.Register(owner);
                }
            });
            VFGameObject publicOwner = null;
            data.OnAllSetsAvailable(sets => {
                foreach (var set in sets.Where(s => s.isPublic)) {
                    if (publicOwner == null) {
                        publicCalls++;
                        publicOwner = GameObjects.Create("Public handler", bake.bakeRoot);
                    }
                    set.Register(publicOwner);
                }
            });
            data.OnAllSetsAvailable(sets => {
                allCalls++;
                Assert.That(seen, Is.EquivalentTo(sets), "All-set consumers run after the factories");
                Assert.That(sets, Has.Count.EqualTo(2));
                Assert.That(sets.SelectMany(s => s.sender), Does.Contain("late"));
            });
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var on = builder.Build(Action(obj, publicRequested, "late"), false);
            _ = builder.Build(Action(obj, publicRequested, "late"), false);
            Assert.That(seen, Is.Empty, "Requests do not build handlers early");
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(allCalls, Is.EqualTo(1));
            Assert.That(publicCalls, Is.EqualTo(publicRequested ? 1 : 0));
            Assert.That(owners.Select(o => o.active), Is.EqualTo(new[] { true, false }));
            Sample(on);
            Assert.That(owners.Select(o => o.active), Is.EqualTo(new[] { false, true }));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(allCalls, Is.EqualTo(1), "Finalization is idempotent");
            Assert.That(() => data.OnAllSetsAvailable(_ => { }), Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => data.Default.Register(owners[0]), Throws.Exception.With.Message.Contains("finalized"));
        }

        [Test]
        public void EmptyChannelConfigurationsAreRejected(
            [Values] bool socket, [Values] bool action, [Values("empty", "blank", "disabled")] string kind) {
            UnityEngine.Component component = socket ? root.AddComponent<VRCFuryHapticSocket>() : Plug(root, true);
            var settings = new SpsChannelSettings { useSharedTag = false };
            if (kind != "empty") settings.channels.Add(new SpsChannelSettings.Channel {
                id = kind == "blank" ? "  " : "custom", allowSelf = false, allowOthers = kind == "blank"
            });
            var service = injector.GetService<SpsChannelsService>();
            System.Action build;
            if (action) {
                var request = new ChangeSpsChannelsAction { target = root, channelSettings = settings };
                build = () => injector.GetService<ChangeSpsChannelsService>().Request(request);
            } else {
                if (component is VRCFuryHapticPlug plug) plug.channelSettings = settings;
                else ((VRCFuryHapticSocket)component).channelSettings = settings;
                build = () => service.Get(component);
            }
            var error = Assert.Throws<System.Exception>(() => build());
            Assert.That(error.Message, Does.Contain("SPS requires at least one channel"));
            Assert.That(error.Message, Does.Contain(root.name));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CommonFinalizationNeedsNoAvatarServicesOrChannelActions(bool socket, bool shared) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var common = new VRCFuryInjector();
            common.ImportScan(typeof(VFServiceAttribute));
            GameObject obj = GameObjects.Create("Detached", root);
            SpsChannelContacts data;
            if (socket) {
                var component = obj.AddComponent<VRCFuryHapticSocket>();
                component.channelSettings.useSharedTag = shared;
                component.addLight = VRCFuryHapticSocket.AddLight.Hole;
                component.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "default" });
                var bake = common.GetService<VRCFuryHapticSocketBaker>().Bake(component, tpsDepthAnimationCompatibility: true);
                var receivers = bake.plugReceivers;
                receivers.Find(true, false); receivers.Find(false, false);
                // Worlds' Udon builder has its own injector, but uses the baker's receiver collection.
                injector.GetService<SpsSocketReceiverService>().GetLegacyWidthReceiver(receivers).Find(false, false);
                data = bake.channelContacts;
            } else {
                var bake = common.GetService<VRCFuryHapticPlugBaker>().Bake(Plug(obj, shared));
                bake.socketReceivers.Find(true, false); bake.socketReceivers.Find(false, false);
                data = bake.channelContacts;
            }
            Assert.That(obj.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty,
                "SDK receivers are created by the final channel callbacks");
            common.GetService<SpsChannelsService>().Apply(data);
            var contacts = obj.GetComponentsInChildren<VRC.Dynamics.ContactBase>(true);
            Assert.That(contacts.OfType<VRCContactReceiver>().All(c => c.transform.parent.name != "Channels"), Is.True,
                "A single configuration needs no channel-selection children");
            Assert.That(contacts.Any(c => c.collisionTags.Contains(SpsTags.LegacyPlugWidth)),
                Is.EqualTo(shared));
            Assert.That(obj.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(socket && shared ? 2 : 0));
            Assert.DoesNotThrow(() => common.GetService<SpsChannelsService>().Apply(data));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DetachedFinalizationSamplesDefaultsWithoutAnAnimator(bool inactive) {
            var detached = new GameObject("Detached without Animator");
            assets.Add(detached);
            detached.SetActive(!inactive);
            var socket = detached.AddComponent<VRCFuryHapticSocket>();
            var common = new VRCFuryInjector();
            common.ImportScan(typeof(VFServiceAttribute));
            var service = common.GetService<SpsChannelsService>();
            var data = service.Get(socket);
            var alternative = data.Request(new SpsChannelSettings {
                useSharedTag = false, channels = { new SpsChannelSettings.Channel { id = "alternative" } }
            });
            var defaultObject = GameObjects.Create("Default", detached);
            var alternativeObject = GameObjects.Create("Alternative", detached);
            defaultObject.active = false;
            data.Default.Register(defaultObject);
            alternative.Register(alternativeObject);
            data.Default.clip.SetScale(defaultObject, Vector3.one * 2);
            Assert.That(detached.GetComponentsInChildren<Animator>(true), Is.Empty);

            service.Apply(data);

            Assert.That(defaultObject.active, Is.True);
            Assert.That(alternativeObject.active, Is.False);
            Assert.That(defaultObject.localScale, Is.EqualTo(Vector3.one * 2));
            Assert.That(detached.GetComponentsInChildren<Animator>(true), Is.Empty);
        }
    }
}
