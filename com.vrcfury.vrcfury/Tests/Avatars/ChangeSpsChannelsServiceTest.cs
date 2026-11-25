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
    using VRC.Dynamics;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.SDK3.Dynamics.PhysBone.Components;
    using Object = UnityEngine.Object;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

    internal class ChangeSpsChannelsServiceTest : SpsChannelBuildTestBase {
        [Test]
        public void ChannelPermissionsApplyInBothDirectionsWithoutExtraContacts(
            [Values(false, true)] bool plugSelf, [Values(false, true)] bool plugOthers,
            [Values(false, true)] bool socketSelf, [Values(false, true)] bool socketOthers) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject plugObj = GameObjects.Create("Plug", root);
            var plug = Plug(plugObj, false);
            plug.channelSettings.channels.Clear();
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel {
                id = "hipsback", allowSelf = plugSelf, allowOthers = plugOthers
            });
            if (!plugSelf && !plugOthers) {
                Assert.Throws<System.Exception>(() => BakePlug(plug));
                return;
            }
            var plugBake = BakePlug(plug);
            GameObject socketObj = GameObjects.Create("Socket", root);
            var socket = socketObj.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel {
                id = "hipsback", allowSelf = socketSelf, allowOthers = socketOthers
            });
            if (!socketSelf && !socketOthers) {
                Assert.Throws<System.Exception>(() => injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root));
                return;
            }
            var socketBake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            var plugReceivers = new[] { plugBake.socketReceivers.Find(true, false), plugBake.socketReceivers.Find(false, false) };
            var socketReceivers = new[] { socketBake.plugReceivers.Find(true, false), socketBake.plugReceivers.Find(false, false) };
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var plugSenders = plugBake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Where(c => c.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug("")))).ToArray();
            var socketSenders = socketBake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Where(c => c.collisionTags.Any(t => t.StartsWith(SpsTags.FormatSocket("")))).ToArray();
            Assert.That(plugSenders.Length, Is.EqualTo(plugSelf || plugOthers ? 1 : 0));
            Assert.That(socketSenders.Length, Is.EqualTo(socketSelf || socketOthers ? 1 : 0));
            for (var party = 0; party < 2; party++) {
                var expected = party == 0 ? plugSelf && socketSelf : plugOthers && socketOthers;
                var plugTags = plugSenders.SelectMany(c => c.collisionTags);
                var socketTags = socketSenders.SelectMany(c => c.collisionTags);
                Assert.That(plugTags.Intersect(socketReceivers[party].component.collisionTags).Any(), Is.EqualTo(expected),
                    "Socket depth must respect the plug's and socket's permissions, party=" + party);
                Assert.That(socketTags.Intersect(plugReceivers[party].component.collisionTags).Any(), Is.EqualTo(expected),
                    "Plug deformation/depth must respect the socket's and plug's permissions, party=" + party);
            }
        }

        [Test]
        public void ChannelChangesSwitchSelfAndOthersPermissions() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Socket", root);
            var socket = obj.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "hipsback", allowSelf = false });
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            RegisterTree(socket, bake.worldSpace);
            bake.plugReceivers.Find(true, false);
            bake.plugReceivers.Find(false, false);
            var clip = Track(injector.GetService<ChangeSpsChannelsActionBuilder>().Build(new ChangeSpsChannelsAction {
                target = obj, channelSettings = new SpsChannelSettings {
                    useSharedTag = false, channels = { new SpsChannelSettings.Channel { id = "hipsback", allowOthers = false } }
                }
            }, false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var off = Defaults(clip);
            foreach (var (motion, self) in new[] { (clip, true), (off, false), (clip, true) }) {
                Sample(motion);
                var sender = obj.GetComponentsInChildren<VRCContactSender>().Single();
                Assert.That(sender.collisionTags, Does.Contain(SpsTags.FormatSocket(self ? SelfTag("hipsback") : "hipsback")));
                Assert.That(sender.collisionTags, Does.Not.Contain(SpsTags.FormatSocket(self ? "hipsback" : SelfTag("hipsback"))));
                var receivers = obj.GetComponentsInChildren<VRCContactReceiver>();
                Assert.That(receivers.Single(r => r.allowSelf).collisionTags,
                    Is.EquivalentTo(self ? new[] { SpsTags.FormatPlug(SelfTag("hipsback")) } : System.Array.Empty<string>()));
                Assert.That(receivers.Single(r => r.allowOthers).collisionTags,
                    Is.EquivalentTo(self ? System.Array.Empty<string>() : new[] { SpsTags.FormatPlug("hipsback") }));
            }
        }

        [Test]
        public void RegisteredObjectsAreEnabledOnlyForSetsThatIncludeThem() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var data = BakePlug(Plug(obj, false)).channelContacts;
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var a = builder.Build(Action(obj, false, "a"), false);
            var b = builder.Build(Action(obj, false, "b"), false);
            var shared = GameObjects.Create("Shared by alternatives", obj);
            var aOnly = GameObjects.Create("Only A", obj);
            var defaultOnly = GameObjects.Create("Only default", obj);
            data.OnAllSetsAvailable(sets => {
                sets[0].Register(defaultOnly);
                sets[1].Register(shared);
                sets[1].Register(aOnly);
                sets[1].Register(aOnly); // Repeated registration is harmless.
                sets[2].Register(shared);
            });
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(defaultOnly.active, Is.True);
            Assert.That(shared.active || aOnly.active, Is.False);
            var aParameter = a.GetFloatBindings().Single().propertyName;
            var bParameter = b.GetFloatBindings().Single().propertyName;
            RunAnimation(VFClip.Create(), (graph, playable) => {
                foreach (var (chooseA, chooseB) in new[] {
                             (false, false), (true, false), (false, true), (true, false), (false, false), (false, true) }) {
                    playable.SetFloat(aParameter, chooseA ? 1 : 0);
                    playable.SetFloat(bParameter, chooseB ? 1 : 0);
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                    Assert.That(shared.active, Is.EqualTo(chooseA || chooseB));
                    Assert.That(aOnly.active, Is.EqualTo(chooseA));
                    Assert.That(defaultOnly.active, Is.EqualTo(!chooseA && !chooseB));
                }
            });
        }

        [Test]
        public void IndependentRequestsHaveDiscretePriorityWithoutEnablingUnrequestedSets() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Requests", root);
            BakePlug(Plug(obj, false));
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var a = builder.Build(Action(obj, false, "a"), false);
            var b = builder.Build(Action(obj, false, "b"), false);
            _ = builder.Build(Action(obj, false, "unused"), false);
            var pa = a.GetFloatBindings().Single().propertyName;
            var pb = b.GetFloatBindings().Single().propertyName;
            injector.GetService<ChangeSpsChannelsService>().Apply();
            RunAnimation(VFClip.Create(), (graph, playable) => {
                foreach (var (va, vb, expected) in new[] {
                             (1f, 0f, "a"), (1f, 1f, "b"), (0f, 0f, "old"),
                             (0f, 0.49f, "old"), (0f, 0.5f, "b"), (0.51f, 0.49f, "a") }) {
                    playable.SetFloat(pa, va);
                    playable.SetFloat(pb, vb);
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                    var sender = obj.GetComponentsInChildren<VRCContactSender>()
                        .Single(s => s.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug(""))));
                    Assert.That(sender.collisionTags, Does.Contain(SpsTags.FormatPlug(expected)));
                }
            });
        }

        [Test]
        public void UnbuiltActionsDoNotCreateContactVariants() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var plug = Plug(obj, false);
            var used = Action(obj, false, "used");
            _ = Action(obj, true, "unused");
            BakePlug(plug);
            Track(injector.GetService<ChangeSpsChannelsActionBuilder>().Build(used, false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var senders = obj.GetComponentsInChildren<VRCContactSender>(true);
            Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.FormatPlug("used"))), Is.True);
            Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.FormatPlug("unused"))), Is.False);
            Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugWidth)
                || s.collisionTags.Contains(SpsTags.LegacyPlugRoot)), Is.False);
        }

        [Test]
        public void AcceptsChannelsRequestedAfterBaking() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            BakePlug(Plug(obj, false));
            var action = new ChangeSpsChannelsAction {
                target = obj, channelSettings = { channels = { new SpsChannelSettings.Channel { id = "late" } } }
            };
            var clip = Track(injector.GetService<ChangeSpsChannelsActionBuilder>().Build(action, false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Sample(clip);
            Assert.That(obj.GetComponentsInChildren<VRCContactSender>().Any(s =>
                s.collisionTags.Contains(SpsTags.FormatPlug("late"))), Is.True);
            Assert.That(() => injector.GetService<ChangeSpsChannelsActionBuilder>().Build(action, false),
                Throws.Exception.With.Message.Contains("finalized"));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SwitchesContactsAndRestoresOriginalChannels(bool socket, bool shared) {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            Assert.Ignore(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#else
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Target", root);
            var action = Action(obj, !shared);
            SelfAndOthersReceiverService.Receivers main;
            if (socket) {
                var component = obj.AddComponent<VRCFuryHapticSocket>();
                component.channelSettings.useSharedTag = shared;
                component.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "old" });
                var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(component, avatarRoot: root);
                RegisterTree(component, bake.worldSpace);
                main = injector.GetService<SpsSocketReceiverService>().GetReceivers(component, bake.worldSpace, "Test");
                var width = injector.GetService<SpsSocketReceiverService>().GetLegacyWidthReceiver(main);
                injector.GetService<SelfAndOthersReceiverAnimatorService>().GetParameter(width, Party.Others);
            } else {
                main = BakePlug(Plug(obj, shared)).socketReceivers;
            }
            var receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            var merged = receiverAnimator.GetParameter(main, Party.Merged);
            var distance = receiverAnimator.GetParameter(main, Party.Others, suffix: ContactReceiver.PARAM_POINT_SEPARATION);
            // Simulate an already selected Others receiver for the pre-animation channel check.
            main.Find(false, true).obj.active = true;
            var senderCount = obj.GetComponentsInChildren<VRCContactSender>(true).Length;
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var on = Track(builder.Build(action, false));
            var off = Defaults(on);
            injector.GetService<ChangeSpsChannelsService>().Apply();

            void AssertTags(string channel, bool publicChannel) {
                var prefix = socket ? SpsTags.FormatSocket("") : SpsTags.FormatPlug("");
                var activeSenders = obj.GetComponentsInChildren<VRCContactSender>()
                    .Where(s => s.collisionTags.Any(t => t.StartsWith(prefix))).ToArray();
                Assert.That(activeSenders, Has.Length.EqualTo(1));
                var tags = activeSenders[0].collisionTags;
                Assert.That(tags, Does.Contain(prefix + channel));
                Assert.That(tags.Contains(prefix + SpsTags.PublicChannel), Is.EqualTo(publicChannel));
                Assert.That(tags.Contains(socket ? SpsTags.LegacySocket : SpsTags.LegacyPlugSphere), Is.EqualTo(publicChannel));
                var receivers = obj.GetComponentsInChildren<VRCContactReceiver>();
                Assert.That(receivers.Any(r => r.parameter == merged.name), Is.True);
                Assert.That(receivers.All(r => !r.collisionTags.Any(t => t.EndsWith("/" + (channel == "old" ? "new" : "old")))), Is.True);
                if (socket) {
                    Assert.That(receivers.Count(r => r.collisionTags.Contains(SpsTags.LegacyPlugWidth)),
                        Is.EqualTo(publicChannel ? 1 : 0), "The legacy width receiver must follow the public channel");
                } else {
                    foreach (var tag in new[] { SpsTags.LegacyPlugWidth, SpsTags.LegacyPlugRoot })
                        Assert.That(obj.GetComponentsInChildren<VRCContactSender>().Count(s => s.collisionTags.Contains(tag)),
                            Is.EqualTo(publicChannel ? 1 : 0), "Legacy plug senders must follow the public channel");
                }
            }
            void SampleWithContact(VFClip clip) {
                RunAnimation(clip, (graph, playable) => {
                    playable.SetFloat(distance, 0.1f);
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                });
            }
            AssertTags("old", shared);
            SampleWithContact(on);
            AssertTags("new", !shared);
            SampleWithContact(off);
            AssertTags("old", shared);
            Assert.That(obj.GetComponentsInChildren<VRCContactSender>(true).Length, Is.GreaterThan(senderCount));
#endif
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ToggleRestoresChannelsUsingNormalDefaults(bool defaultOn, bool writeDefaults) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            BakePlug(Plug(obj, true));
            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            injector.GetService<GlobalsService>().allFeaturesInRun.Add(new FixWriteDefaults {
                mode = writeDefaults ? FixWriteDefaults.FixWriteDefaultsMode.ForceOn : FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            });
            var defaults = injector.GetService<FixWriteDefaultsService>();
            defaults.DetermineWriteDefaultsStrategy();
            var toggle = injector.CreateAndFillObject<VF.Feature.ToggleBuilder>();
            toggle.featureBaseObject = root;
            toggle.model = new Toggle {
                name = "Channels", paramOverride = "Channels", addMenuItem = false, defaultOn = defaultOn,
                state = new State { actions = { Action(obj, false) } }
            };
            toggle.Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            toggle.ApplyRestingState();
            injector.GetService<RestingStateService>().OnPhaseChanged(VF.Feature.Base.FeatureOrder.ApplyToggleRestingState);
            defaults.RecordAllDefaults();
            defaults.AdjustWriteDefaults();
            injector.GetService<FixEmptyMotionService>().Apply();
            injector.GetService<NoBadControllerParamsService>().Apply();
            var fx = injector.GetService<ControllersService>().GetFx();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller); assets.AddRange(context.NewAssets);
            var graph = PlayableGraph.Create("Channel toggle defaults");
            try {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "Channels", root.AddComponent<Animator>()).SetSourcePlayable(playable);
                graph.Play();
                foreach (var enabled in new[] { defaultOn, !defaultOn, defaultOn, false }) {
                    playable.SetBool("Channels", enabled);
                    for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                    var senders = obj.GetComponentsInChildren<VRCContactSender>()
                        .Where(s => s.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug("")))).ToArray();
                    Assert.That(senders, Has.Length.EqualTo(1));
                    Assert.That(senders[0].collisionTags, Does.Contain(SpsTags.FormatPlug(enabled ? "new" : "old")));
                }
            } finally { graph.Destroy(); }
        }

        [Test]
        public void SharesRadiusAndMergedSelectorBindingsWithoutBufferingMeasurements() {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            Assert.Ignore(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#else
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var action = Action(obj, false);
            var bake = BakePlug(Plug(obj, true));
            var animator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            var merged = animator.GetParameter(bake.socketReceivers, Party.Merged);
            var selfInput = animator.GetParameter(bake.socketReceivers, Party.Self, suffix: ContactReceiver.PARAM_POINT_SEPARATION);
            var othersInput = animator.GetParameter(bake.socketReceivers, Party.Others, suffix: ContactReceiver.PARAM_POINT_SEPARATION);
            var fx = injector.GetService<ControllersService>().GetFx();
            var paramCount = fx.parameters.Count();
            var animation = Track(VFClip.Create());
            animation.SetScale(bake.mainSender, new Vector3(0.03f, 0.015f, 0.4f));
            var mergedSelf = bake.socketReceivers.Find(true, true).obj;
            var mergedOthers = bake.socketReceivers.Find(false, true).obj;
            animation.SetEnabled(mergedSelf, true);
            animation.SetEnabled(mergedOthers, false);
            var on = Track(injector.GetService<ChangeSpsChannelsActionBuilder>().Build(action, false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(fx.parameters.Count(), Is.EqualTo(paramCount + 1), "Only the channel request adds a parameter");
            var senderRadius = bake.mainSender.GetComponentsInSelfAndChildren<VRCContactSender>().First().radius;
            Sample(animation);
            RunAnimation(on, (graph, playable) => {
                playable.SetFloat(selfInput, 0.1f);
                playable.SetFloat(othersInput, 0.3f);
                for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                var active = obj.GetComponentsInChildren<VRCContactReceiver>().Where(r => r.parameter == merged.name).ToArray();
                Assert.That(active, Has.Length.EqualTo(1));
                Assert.That(active[0].allowSelf, Is.True);
                var sphere = obj.GetComponentsInChildren<VRCContactSender>().Single(s => s.collisionTags.Contains(SpsTags.FormatPlug("new")));
                Assert.That(sphere.transform.lossyScale.y, Is.EqualTo(0.015f).Within(0.00001f));
                Assert.That(sphere.radius, Is.EqualTo(senderRadius));
            });
#endif
        }

        [Test]
        public void TwoAlternativesCannotLeaveAnotherContactSetEnabled() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var a = Action(obj, false, "a");
            var b = Action(obj, false, "b");
            BakePlug(Plug(obj, true));
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var onA = Track(builder.Build(a, false));
            var onB = Track(builder.Build(b, false));
            var off = Defaults(onB);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            foreach (var pair in new[] { (onA, "a"), (onB, "b"), (onA, "a"), (off, "old") }) {
                Sample(pair.Item1);
                var senders = obj.GetComponentsInChildren<VRCContactSender>()
                    .Where(s => s.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug("")))).ToArray();
                Assert.That(senders, Has.Length.EqualTo(1));
                Assert.That(senders[0].collisionTags, Does.Contain(SpsTags.FormatPlug(pair.Item2)));
            }
        }

        [Test]
        public void LoopCanSwitchBetweenChannelsRequestedAtDifferentTimes() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            BakePlug(Plug(obj, true));
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var a = builder.Build(Action(obj, false, "a"), false);
            var b = builder.Build(Action(obj, true, "b"), false);
            var loop = Track(injector.GetService<ClipBuilderService>().MergeSingleFrameClips((0, a), (1, b), (2, a)));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            foreach (var (time, channel, legacy) in new[] { (0f, "a", false), (1f, "b", true), (2f, "a", false) }) {
                Sample(loop, time);
                var senders = obj.GetComponentsInChildren<VRCContactSender>();
                var native = senders.Single(s => s.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug(""))));
                Assert.That(native.collisionTags, Does.Contain(SpsTags.FormatPlug(channel)));
                Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.EqualTo(legacy));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NativeClassificationIncludesChannelsRequestedByLaterActionBuilders(bool socket) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Target", root);
            var action = Action(obj, false);
            SelfAndOthersReceiverService.Receivers main;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            VFAParam measurement;
#endif
            if (socket) {
                var component = obj.AddComponent<VRCFuryHapticSocket>();
                component.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "old" });
                var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(component, avatarRoot: root);
                RegisterTree(component, bake.worldSpace);
                main = injector.GetService<SpsSocketReceiverService>().GetReceivers(component, bake.worldSpace, "Test");
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                measurement = injector.GetService<SpsSocketMeasurementsService>().GetMeasurements(bake, "Test")
                    (Party.Others)(VRCFuryHapticPlugEditor.SpsPlugLengthMeters);
#endif
            } else {
                var bake = BakePlug(Plug(obj, true));
                main = bake.socketReceivers;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                measurement = injector.GetService<SpsPlugMeasurementsService>().GetMeasurements(bake, "Test")
                    (Party.Others)(VRCFuryHapticPlugEditor.SpsInteractingWithLegacy);
#endif
            }
            var originalTags = main.request.getTags(main.request.channels.Default, false).ToArray();
            var animator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            var parameter = animator.GetParameter(main, Party.Others).name;
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var on = Track(builder.Build(action, false));
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            new BlendtreeMath(injector.GetService<ControllersService>().GetFx(), animator.GetTree(main))
                .CopyInPlace(measurement, "Injected measurement");
#endif
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(main.request.getTags(main.request.channels.Default, false), Is.EqualTo(originalTags), "Existing receiver requests are untouched");
            var native = socket ? SpsTags.FormatPlug("new") : SpsTags.FormatSocket("new");
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var fx = injector.GetService<ControllersService>().GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            injector.GetService<NoBadControllerParamsService>().Apply();
            injector.GetService<FixPartiallyWeightedAapsService>().Apply();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller); assets.AddRange(context.NewAssets);
            var graph = PlayableGraph.Create("Channel decoding");
            try {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "Channels", root.AddComponent<Animator>()).SetSourcePlayable(playable);
                graph.Play();
                var requestParameter = on.GetFloatBindings().Single(b => b.IsAnimatorBinding()).propertyName;
                playable.SetFloat(requestParameter, 1);
                playable.SetFloat(parameter, 1);
                playable.SetFloat(parameter + "_PointSeparation", 0.2f);
                if (socket) playable.SetFloat(parameter + "_SenderLossyScaleZ", 0.4f);
                playable.SetFloat(parameter + "_CollisionTag_" + native, 1);
                for (var i = 0; i < 12; i++) graph.Evaluate(1f / 60);
                Assert.That(obj.GetComponentsInChildren<VRCContactReceiver>().Any(r => r.collisionTags.Contains(native)), Is.True);
                Assert.That(playable.GetFloat(measurement), Is.EqualTo(socket ? 0.4f : 0).Within(0.00001f),
                    "A newly requested native tag must feed the shared classification");
                Assert.That(playable.GetFloat("Injected measurement"), Is.EqualTo(playable.GetFloat(measurement)).Within(0.00001f));
                if (socket) {
                    playable.SetFloat(parameter + "_SenderLossyScaleZ", 0.6f);
                    graph.Evaluate(1f / 60);
                    Assert.That(playable.GetFloat(measurement), Is.EqualTo(0.6f).Within(0.00001f),
                        "Steady native classification must not buffer the continuous dimension input");
                }
                playable.SetFloat(requestParameter, 0);
                playable.SetFloat(parameter + "_CollisionTag_" + native, 0);
                for (var i = 0; i < 12; i++) graph.Evaluate(1f / 60);
                Assert.That(playable.GetFloat(measurement), Is.EqualTo(socket ? 0.2f : 1).Within(0.00001f),
                    "Existing legacy decoding still works");
            } finally { graph.Destroy(); }
#endif
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void PrivateComponentsCanGainLegacyDecodingFromALaterPublicRequest(bool socket, bool publicRequested) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Private target", root);
            SelfAndOthersReceiverService.Receivers main;
            System.Func<string, VFAParam> measurements;
            if (socket) {
                var component = obj.AddComponent<VRCFuryHapticSocket>();
                component.channelSettings.useSharedTag = false;
                component.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "old" });
                var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(component, avatarRoot: root);
                RegisterTree(component, bake.worldSpace);
                main = bake.plugReceivers;
                measurements = injector.GetService<SpsSocketMeasurementsService>().GetMeasurements(bake, "Test")(Party.Others);
                _ = measurements(VRCFuryHapticPlugEditor.SpsPlugLengthMeters);
                _ = measurements(VRCFuryHapticPlugEditor.SpsPlugRadiusMeters);
            } else {
                var bake = BakePlug(Plug(obj, false));
                main = bake.socketReceivers;
                measurements = injector.GetService<SpsPlugMeasurementsService>().GetMeasurements(bake, "Test")(Party.Others);
            }
            var legacy = measurements(VRCFuryHapticPlugEditor.SpsInteractingWithLegacy);
            var input = injector.GetService<SelfAndOthersReceiverAnimatorService>().GetParameter(main, Party.Others);
            // The measurement already exists before the final public/private choice is requested.
            var on = injector.GetService<ChangeSpsChannelsActionBuilder>().Build(Action(obj, publicRequested), false);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            RunAnimation(on, (graph, playable) => {
                playable.SetFloat(input, 1);
                playable.SetFloat(input.name + "_PointSeparation", 0.2f);
                for (var i = 0; i < 8; i++) graph.Evaluate(1f / 60);
                Assert.That(playable.GetFloat(legacy), Is.EqualTo(publicRequested ? 1 : 0));
            });
        }
#endif
        [TestCase(false)]
        [TestCase(true)]
        public void ChannelChangesRespectLegacyMode(bool originalShared) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Socket", root);
            var socket = obj.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = originalShared;
            socket.channelSettings.channels.Add(new SpsChannelSettings.Channel { id = "default" });
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            var action = Action(obj, !originalShared);
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket,
                tpsDepthAnimationCompatibility: true, avatarRoot: root);
            RegisterTree(socket, bake.worldSpace);
            var options = injector.GetService<SpsOptionsService>();
            bake.channelContacts.OnAllSetsAvailable(_ => {
                foreach (var legacy in new[] { bake.legacyLights, bake.legacyFrontSender }.Where(o => o != null))
                    options.RegisterLegacyObject(legacy);
            });
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var on = Track(builder.Build(action, false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var request = on.GetFloatBindings().Single(b => b.IsAnimatorBinding()).propertyName;
            RunAnimation(VFClip.Create(), (graph, playable) => {
                foreach (var (changed, legacy, expected) in new[] {
                             (true, false, false), (true, true, !originalShared), (false, true, originalShared) }) {
                    playable.SetFloat(request, changed ? 1 : 0);
                    playable.SetFloat(options.GetLegacyParameter(), legacy ? 1 : 0);
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                    Assert.That(obj.GetComponentsInChildren<Light>().Length, Is.EqualTo(expected ? 2 : 0));
                    Assert.That(obj.GetComponentsInChildren<VRCContactSender>().Any(s => s.collisionTags.Contains(SpsTags.LegacySocketFront)),
                        Is.EqualTo(expected));
                }
            });
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [Test]
        public void TipLightRequiresPublicChannelsAndLegacyMenuToggle(
            [Values(false, true)] bool originalShared, [Values(false, true)] bool legacyDefaultOn,
            [Values(false, true)] bool writeDefaults) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            Plug(obj, originalShared);
            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.addOtherFeature = _ => { };
            globals.allFeaturesInRun.Add(new SpsOptions {
                autoModeAvailable = false, addDpsTipLight = true, legacySocketCompatibility = true,
                legacyModeEnabledOnAvatarLoad = legacyDefaultOn
            });
            globals.allFeaturesInRun.Add(new FixWriteDefaults {
                mode = writeDefaults ? FixWriteDefaults.FixWriteDefaultsMode.ForceOn : FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            });
            var defaults = injector.GetService<FixWriteDefaultsService>();
            defaults.DetermineWriteDefaultsStrategy();
            var plugs = injector.GetService<BakeHapticPlugsService>();
            plugs.ApplyEarlyBake();
            plugs.Apply();
            var toggle = injector.CreateAndFillObject<VF.Feature.ToggleBuilder>();
            toggle.featureBaseObject = root;
            toggle.model = new Toggle {
                name = "Channels", paramOverride = "Channels",
                state = new State { actions = { Action(obj, !originalShared) } }
            };
            toggle.Apply();
            // A second public set must share the tip light created for the first one.
            injector.GetService<ChangeSpsChannelsActionBuilder>().Build(Action(obj, true, "another-public"), false);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            animatorPrepared = true;
            toggle.ApplyRestingState();
            injector.GetService<RestingStateService>().OnPhaseChanged(VF.Feature.Base.FeatureOrder.ApplyToggleRestingState);
            defaults.RecordAllDefaults();
            defaults.AdjustWriteDefaults();
            injector.GetService<FixEmptyMotionService>().Apply();

            var options = injector.GetService<SpsOptionsService>();
            options.MoveMenus();
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu();
            var channelParameter = menu.controls.Single(c => c.name == "Channels").parameter.name;
            var legacyParameter = menu.controls.Single(c => c.name == "SPS").subMenu.controls
                .Single(c => c.name.Contains("Legacy Support")).parameter.name;
            var tip = obj.GetComponentsInChildren<Light>(true).Single(l => l.name == "Light - Legacy DPS Trigger");
            RunAnimation(VFClip.Create(), (graph, playable) => {
                for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                Assert.That(tip.isActiveAndEnabled, Is.EqualTo(originalShared && legacyDefaultOn),
                    "Initial tip state must respect both menu defaults");
                foreach (var (changed, legacy) in new[] {
                             (false, false), (true, false), (true, true), (false, true),
                             (true, true), (true, false), (false, false), (false, true), (true, true) }) {
                    playable.SetBool(channelParameter, changed);
                    playable.SetFloat(legacyParameter, legacy ? 1 : 0);
                    for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                    var isPublic = changed ? !originalShared : originalShared;
                    Assert.That(tip.isActiveAndEnabled, Is.EqualTo(isPublic && legacy),
                        $"Changed channels={changed}, public={isPublic}, legacy={legacy}");
                    Assert.That(obj.GetComponentsInChildren<Light>(true), Has.Length.EqualTo(1),
                        "Switching channels and legacy mode must reuse the same tip light");
                }
            });
        }
#endif

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(false)]
        [TestCase(true)]
        public void MaterialSwapsPreserveChannelSettingsWithoutChannelActions(bool shared) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var plug = Plug(obj, shared);
            BakePlug(plug);
            var renderer = GameObjects.Create("Mesh", obj).AddComponent<MeshRenderer>();
            var initial = new Material(Shader.Find("Unlit/Color"));
            assets.Add(initial);
            SpsPatcher.Patch(initial, false, false);
            var replacement = new Material(initial);
            replacement.SetColor("_Color", Color.red);
            assets.Add(replacement);
            var receivers = injector.GetService<SpsPlugReceiverService>();
            // Both materials go through the same configuration as normal and animated SPS materials.
            receivers.ConfigureMaterial(initial, plug);
            receivers.ConfigureMaterial(replacement, plug);
            renderer.sharedMaterial = initial;
            receivers.Create(plug, new[] { renderer });
            var swap = Track(VFClip.Create("Swap SPS material"));
            swap.SetCurve(renderer, "m_Materials.Array.data[0]", new[] {
                new ObjectReferenceKeyframe { time = 0, value = replacement }
            });
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<AvatarBindingStateService>().ApplyModifiedMaterialProperties();
            var expectedChannels = shared ? 2 : 1;
            Assert.That(renderer.sharedMaterial.GetFloat("_SPS_OthersChannelTagCount"), Is.EqualTo(expectedChannels));
            // Build-time SampleAnimation leaves editor-only property blocks. They aren't serialized
            // into the uploaded avatar and must not mask a replacement material's wrong settings.
            renderer.SetPropertyBlock(null);
            renderer.SetPropertyBlock(null, 0);
            RunAnimation(swap, (graph, _) => {
                for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                Assert.That(renderer.sharedMaterial.GetColor("_Color"), Is.EqualTo(Color.red), "The Animator must actually swap materials");
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                float Read(string name) => properties.HasFloat(name) ? properties.GetFloat(name) : renderer.sharedMaterial.GetFloat(name);
                Assert.That(Read("_SPS_OthersChannelTagCount"), Is.EqualTo(expectedChannels),
                    "Swapped materials must use the receiver's final channel layout");
                foreach (var name in new[] { "_SPS_OthersLegacyTags", "_SPS_UseLegacyLights" })
                    Assert.That(Read(name), Is.EqualTo(shared ? 1 : 0), "Swapped materials must preserve public-channel compatibility");
            });
        }
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(false)]
        [TestCase(true)]
        public void SmoothLoopSelectsOneChannelSetBetweenKeyframes(bool publicToPrivate) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var plug = Plug(obj, true);
            BakePlug(plug);
            var renderer = GameObjects.Create("Mesh", obj).AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            SpsPatcher.Patch(material, false, false);
            renderer.sharedMaterial = material;
            injector.GetService<SpsPlugReceiverService>().Create(plug, new[] { renderer });
            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            var a = Action(obj, publicToPrivate, "a");
            a.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "c" });
            var loop = Track(injector.GetService<SmoothLoopActionBuilder>().Build(new SmoothLoopAction {
                loopTime = 2,
                state1 = new State { actions = { a } },
                state2 = new State { actions = { Action(obj, false, "b") } }
            }, injector.GetService<ActionClipService>(), root));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<AvatarBindingStateService>().ApplyModifiedMaterialProperties();
            RunAnimation(loop, (graph, playable) => {
                var errors = new List<string>();
                var properties = new MaterialPropertyBlock();
                foreach (var time in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f }) {
                    playable.SetFloat("Test motion time", time / 2);
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                    var writers = obj.GetComponentsInChildren<VRCContactReceiver>()
                        .Where(r => r.writeToMaterials && r.separateShaderParameter == "SPS_Others").ToArray();
                    var senders = obj.GetComponentsInChildren<VRCContactSender>()
                        .Where(s => s.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug("")))).ToArray();
                    if (writers.Length != 1 || senders.Length != 1) {
                        errors.Add($"At {time}s: {writers.Length} Others receivers and {senders.Length} native senders; expected one of each");
                    }
                    renderer.GetPropertyBlock(properties);
                    float Read(string name) => properties.HasFloat(name) ? properties.GetFloat(name) : renderer.sharedMaterial.GetFloat(name);
                    var channelCount = Read("_SPS_OthersChannelTagCount");
                    if (writers.Any(r => r.collisionTags.Count(t => t.StartsWith(SpsTags.FormatSocket(""))) != channelCount)) {
                        errors.Add($"At {time}s: shader channel count {channelCount} does not match its active receiver");
                    }
                    foreach (var name in new[] { "_SPS_OthersLegacyTags", "_SPS_UseLegacyLights" }) {
                        var legacy = Read(name);
                        if (legacy != 0 && legacy != 1) errors.Add($"At {time}s: {name} is interpolated to {legacy}");
                    }
                }
                Assert.That(errors, Is.Empty, "Channels and their shader metadata must switch as one discrete configuration");
            }, motionTime: true);
        }
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void ShaderTagLayoutFollowsTheSelectedSetWithoutEnablingThePlug() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var plug = Plug(obj, true);
            var bake = BakePlug(plug);
            var action = Action(obj, false);
            var renderer = GameObjects.Create("Mesh", root).AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            SpsPatcher.Patch(material, false, false);
            renderer.sharedMaterial = material;
            injector.GetService<SpsPlugReceiverService>().Create(plug, new[] { renderer });
            var builder = injector.GetService<ChangeSpsChannelsActionBuilder>();
            var on = Track(builder.Build(action, false));
            var off = Defaults(on);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            foreach (var pair in new[] { (on, 1f, 0f), (off, 2f, 1f) }) {
                Sample(pair.Item1);
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_SPS_OthersChannelTagCount"), Is.EqualTo(pair.Item2));
                foreach (var property in new[] { "_SPS_OthersLegacyTags", "_SPS_UseLegacyLights" })
                    Assert.That(properties.GetFloat(property), Is.EqualTo(pair.Item3));
            }
            obj.SetActive(false);
            Sample(on);
            Assert.That(root.GetComponentsInChildren<VRCContactSender>(), Is.Empty, "Changing channels must not enable the plug itself");
        }
#endif
    }
}
