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

    internal class BakeHapticSocketsServiceTest : SpsAvatarBuildTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void SocketBakeProtectsGuidedPathSourcesFromHeadChop(bool disabled) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var head = GameObjects.Create("Head", root);
            var socket = GameObjects.Create("Socket", head).AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.addMenuItem = false;
            var stop = GameObjects.Create("Stop", head);
            stop.active = !disabled;
            var outsideHead = GameObjects.Create("Other Stop", root);
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = outsideHead });
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = false } };
            var added = new List<FeatureModel>();
            globals.addOtherFeature = added.Add;
            injector.GetService<BakeHapticSocketsService>().Apply();
            var exclusions = added.OfType<ShowInFirstPerson>().ToArray();
            var baked = socket.owner().Children().Single(child => child.name == "BakedSpsSocket");
            var paths = new[] { stop, outsideHead }.Select(source => source.Children().Single(child => child.name == "BakedSpsPath")).ToArray();
            Assert.That(exclusions.Select(e => e.objOverride), Is.EquivalentTo(new[] { baked }.Concat(paths).Select(obj => (GameObject)obj)));
            Assert.That(paths.All(path => path.localPosition == Vector3.zero && path.localRotation == Quaternion.identity
                && path.localScale == Vector3.one), Is.True);
            Assert.That(exclusions.Any(e => e.objOverride == (GameObject)stop || e.objOverride == (GameObject)outsideHead), Is.False,
                "Authored stop objects must retain their own head-chop behavior");
            Assert.That(exclusions.All(e => e.useObjOverride && e.onlyIfChildOfHead), Is.True,
                "Only stop sources under the avatar head should receive first-person exclusions");
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var guideSenders = baked.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Where(sender => sender.name.StartsWith("Sender - Path Stop")).ToArray();
            Assert.That(guideSenders, Has.Length.EqualTo(2));
            Assert.That(guideSenders.SelectMany(sender => sender.owner().GetConstraints().Single(c => c.IsParent()).GetSources()),
                Is.EquivalentTo(paths));
        }

        [Test]
        public void SocketLocalMeasurementsUseProtectedBakeScale(
            [Values(0.5f, 2f)] float authoredScale,
            [Values] bool nonUniform
        ) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var owner = GameObjects.Create("Authored socket", root);
            owner.localScale = new Vector3(authoredScale, nonUniform ? authoredScale * 1.5f : authoredScale, authoredScale);
            var socket = owner.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var bake = injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket, avatarRoot: root);
            var outputs = injector.GetService<SpsSocketMeasurementsService>()
                .GetMeasurements(bake, "Head socket")(Party.Others);
            var meters = outputs(SpsPlugEntryRadiusMeters);
            var local = outputs(SpsPlugEntryRadiusLocal);
            var detector = injector.GetService<WorldScaleDetectorService>();
            var cache = (Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector);
            Assert.That(cache.ContainsKey(bake.bakeRoot), Is.True,
                "Local-unit conversion must measure the head-chop-protected bake root");
            Assert.That(cache.ContainsKey(owner), Is.False);
            Assert.That(bake.bakeRoot.worldScale.x, Is.EqualTo(authoredScale).Within(.00001f));
            Assert.That(bake.worldSpace.worldScale, Is.EqualTo(Vector3.one));
            var raw = cache[bake.bakeRoot].raw;
            Start();
            var receiver = Main(false);
            Set(receiver, .5f, .4196f, length: .4f, radius: .02f, native: true);
            foreach (var resize in new[] { 1f, 2f }) {
                foreach (var chopped in new[] { false, true }) {
                    // Simulate the scale split: authored hierarchy is chopped, baked data stays visible.
                    owner.localScale = Vector3.one * (authoredScale * resize * (chopped ? .0001f : 1));
                    bake.bakeRoot.worldScale = Vector3.one * authoredScale * resize;
                    bake.worldSpace.worldScale = Vector3.one;
                    playable.SetFloat(raw, resize / WorldScaleDetectorService.MaxDetectedScale);
                    Evaluate(30);
                    Assert.That(playable.GetFloat(meters), Is.EqualTo(.0004f).Within(.00001f));
                    Assert.That(playable.GetFloat(local), Is.EqualTo(.0004f / (authoredScale * resize)).Within(.00001f));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutoSelectionHonorsPerSocketSetting(bool includeAnotherSocket) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var excluded = GameObjects.Create("Excluded", root).AddComponent<VRCFuryHapticSocket>();
            excluded.name = "Excluded";
            excluded.addLight = VRCFuryHapticSocket.AddLight.Hole;
            excluded.enableAuto = false;
            VRCFuryHapticSocket included = null;
            if (includeAnotherSocket) {
                included = GameObjects.Create("Included", root).AddComponent<VRCFuryHapticSocket>();
                included.name = "Included";
                included.addLight = VRCFuryHapticSocket.AddLight.Hole;
            }
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = true, legacyModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            injector.GetService<BakeHapticSocketsService>().Apply();
            Assert.That(SocketMenuParameter("Excluded"), Is.Not.Null, "Disabling auto must preserve the manual menu toggle");
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).SingleOrDefault(r => r.name == "SpsAutoDistance");
            if (included == null) {
                Assert.That(receiver, Is.Null);
                Assert.That(fx.parameters.Any(p => p.name.EndsWith("_SpsAutoMode")), Is.False);
            } else {
                Assert.That(receiver, Is.Not.Null);
                var bakeRoot = included.owner().Children().Single(child => child.name == "BakedSpsSocket");
                var receiverFrame = bakeRoot.Children().Single(child => child.name == "WorldSpace");
                Assert.That(receiver.owner().GetConstraints().Single(c => c.IsParent()).GetSources(), Is.EqualTo(new[] { receiverFrame }));
            }
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        public void AutoSelectionUsesCloseContactHysteresisAndLocksActiveSocket(bool remote, bool manualSocket, bool sharedBone) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            root.GetComponent<VRCAvatarDescriptor>().ViewPosition = new Vector3(0, 1.8f, 0);
            foreach (var name in new[] { "SocketA", "SocketB", "NoMenu" }) {
                var socket = GameObjects.Create(name, root).AddComponent<VRCFuryHapticSocket>();
                socket.name = name;
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.addMenuItem = name != "NoMenu";
            }
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = true, legacyModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            VFAParam plugOn = null;
            if (sharedBone) {
                injector.Set("autoChannelGenerator", new AutoSelectionBones());
                var plugOwner = GameObjects.Create("Plug", root);
                var plug = plugOwner.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = .4f;
                plug.radius = .04f;
                injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
                plugOn = fx.GetOrCreateFloat("Plug On", 1);
                var on = VFClip.Create("Plug On");
                var off = VFClip.Create("Plug Off");
                on.SetEnabled(plugOwner, true);
                off.SetEnabled(plugOwner, false);
                tree.Add(BlendtreeMath.GreaterThan(plugOn, .5f).create(on, off));
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.gameObject.name == "SpsAutoDistance");
            Assert.That(receiver.collisionTags, Is.EqualTo(new[] { SpsTags.PlugClose }));
            Assert.That(receiver.radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(receiver.allowOthers && receiver.allowSelf && receiver.localOnly, Is.True);
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.None));
            Assert.That(receiver.owner().GetConstraints().Single(c => c.IsParent()).GetSources(), Has.Length.EqualTo(2));
            Assert.That(fx.parameters.Any(p => p.name.EndsWith("_AutoActiveDist") || p.name.EndsWith("_AutoCloser")
                || p.name.EndsWith("_AutoSocketNum") || p.name == receiver.parameter + "_PointSeparation"), Is.False);
            var a = SocketMenuParameter("SocketA");
            var b = SocketMenuParameter("SocketB");
            var auto = injector.GetService<SpsOptionsService>().GetAutoParameter();
            var layer = fx.GetLayers().Single(l => l.name.EndsWith("SPS - Auto Socket Selection"));
            var states = layer.allStates.ToDictionary(s => Animator.StringToHash(s.name));
            Start();
            var layerIndex = fx.GetLayers().ToList().IndexOf(layer);
            var lastState = 0;
            var proximityA = 0.5f;
            var proximityB = 0.5f;
            string CurrentState() => states.GetOrDefault(playable.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash)?.name ?? "";
            void Tick(int frames) {
                for (var frame = 0; frame < frames; frame++) {
                    var stateName = CurrentState();
                    playable.SetFloat(receiver.parameter, stateName.EndsWith("SocketA") ? proximityA
                        : stateName.EndsWith("SocketB") ? proximityB : 0);
                    Evaluate(1);
                    var state = playable.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash;
                    if (state == lastState) continue;
                    lastState = state;
                    // Unity runs the state machine; emulate VRChat's Set drivers on state entry.
                    if (!states.TryGetValue(state, out var entered)) continue;
                    foreach (var parameter in entered.behaviours.GetBehaviours<VRCAvatarParameterDriver>().SelectMany(d => d.parameters)) {
                        Assert.That(parameter.type, Is.EqualTo(VRC_AvatarParameterDriver.ChangeType.Set));
                        playable.SetFloat(parameter.name, parameter.value);
                    }
                }
            }
            playable.SetFloat("IsLocal", remote ? 0 : 1);
            if (manualSocket) playable.SetFloat(a, 1);
            playable.SetFloat(auto, 1);
            Tick(120);
            Assert.That(playable.GetFloat(a), Is.EqualTo(manualSocket ? 1 : 0));
            Assert.That(playable.GetFloat(b), Is.Zero);
            Assert.That(receiver.gameObject.activeSelf, Is.EqualTo(!remote));
            if (remote) return;
            if (sharedBone) {
                proximityA = proximityB = 1;
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.Zero, "An active plug on the same bone blocks this socket");
                Assert.That(playable.GetFloat(b), Is.EqualTo(1), "A socket on a different bone remains eligible");
                playable.SetFloat(plugOn, 0);
                proximityB = 0;
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1), "The socket becomes eligible when the plug's parent turns off");
                Assert.That(playable.GetFloat(b), Is.Zero);
                return;
            }
            if (manualSocket) {
                proximityA = 1;
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1));
                Assert.That(playable.GetFloat(b), Is.Zero);
                Assert.That(CurrentState(), Is.Not.EqualTo("Switch To SocketA"), "Auto must not claim an already-enabled socket");
                playable.SetFloat(auto, 0);
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1), "Turning Auto off while scanning must preserve manual sockets");
                playable.SetFloat(auto, 1);
                proximityB = 1;
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1), "Acquiring another socket must not turn manual sockets off");
                Assert.That(playable.GetFloat(b), Is.EqualTo(1));
                proximityB = 0.009f;
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1));
                Assert.That(playable.GetFloat(b), Is.Zero, "Leaving contact must only release the Auto-owned socket");
                proximityB = 1;
                Tick(120);
                playable.SetFloat(auto, 0);
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1));
                Assert.That(playable.GetFloat(b), Is.Zero, "Turning Auto off must only release its own socket");
                playable.SetFloat(auto, 1);
                Tick(120);
                Assert.That(playable.GetFloat(b), Is.EqualTo(1));
                playable.SetFloat(b, 0);
                proximityB = 0;
                Tick(120);
                playable.SetFloat(b, 1);
                playable.SetFloat(auto, 0);
                Tick(120);
                Assert.That(playable.GetFloat(a), Is.EqualTo(1));
                Assert.That(playable.GetFloat(b), Is.EqualTo(1), "Manual off/on must relinquish Auto ownership");
                return;
            }
            for (var frame = 0; frame < 120 && CurrentState() != "Settle SocketA"; frame++) Tick(1);
            Assert.That(CurrentState(), Is.EqualTo("Settle SocketA"));
            proximityA = 0.51f;
            proximityB = 1;
            Tick(120);
            Assert.That(playable.GetFloat(a), Is.EqualTo(1));
            Assert.That(playable.GetFloat(b), Is.Zero);
            proximityA = 0.01f;
            Tick(120);
            Assert.That(CurrentState(), Is.EqualTo("Switch To SocketA"), "Even a stronger nearby contact must not steal the socket");
            Assert.That(playable.GetFloat(a), Is.EqualTo(1));
            Assert.That(playable.GetFloat(b), Is.Zero);
            proximityA = 0.009f;
            Tick(120);
            Assert.That(playable.GetFloat(a), Is.Zero);
            Assert.That(playable.GetFloat(b), Is.EqualTo(1));
            proximityA = 1;
            proximityB = 0.02f;
            Tick(120);
            Assert.That(CurrentState(), Is.EqualTo("Switch To SocketB"));
            playable.SetFloat(auto, 0);
            Tick(120);
            Assert.That(playable.GetFloat(a), Is.Zero);
            Assert.That(playable.GetFloat(b), Is.Zero);
            Assert.That(receiver.gameObject.activeSelf, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SocketOgbIdsNormalizeMenuFallbacksAndExplicitOverridesBeforeDeduplication(bool useOverride) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var hapticsEnabled = VF.Menu.HapticsToggleMenuItem.Get();
            if (!hapticsEnabled) Assert.Ignore("Haptics must be enabled for OGB receiver integration tests");
            foreach (var id in new[] { "Front/Socket", "Front_Socket", "Front/Socket" }) {
                var obj = GameObjects.Create(id, root);
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.name = useOverride ? "Menu " + id : id;
                socket.oscId = useOverride ? id : "";
                socket.addMenuItem = false;
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var outputs = root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Select(r => r.parameter).Where(p => p.StartsWith("OGB/Socket/")).ToArray();
            Assert.That(outputs, Does.Contain("OGB/Socket/Front_Socket/DepthSelf"));
            Assert.That(outputs, Does.Contain("OGB/Socket/Front_Socket_2/DepthSelf"));
            Assert.That(outputs, Does.Contain("OGB/Socket/Front_Socket_3/DepthSelf"));
            Assert.That(outputs, Is.Unique);
        }

        [TestCase(HumanBodyBones.Hips, "Test", true)]
        [TestCase(HumanBodyBones.Hips, "Rubbing Job", false)]
        [TestCase(HumanBodyBones.Hips, "Rubbing", false)]
        [TestCase(HumanBodyBones.Hips, "Job", false)]
        [TestCase(HumanBodyBones.Head, "Test", false)]
        [TestCase(HumanBodyBones.Spine, "Test", false)]
        [TestCase(HumanBodyBones.LeftUpperLeg, "Test", false)]
        [TestCase(HumanBodyBones.LastBone, "Test", false)]
        public void SocketTouchReceiversUseSharedEligibility(HumanBodyBones bone, string menuName, bool supportsTouch) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (!VF.Menu.HapticsToggleMenuItem.Get()) Assert.Ignore("OGB haptics are disabled");
            injector.Set("autoChannelGenerator", new TouchBoneChannels(bone == HumanBodyBones.LastBone ? null : bone));
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.name = menuName;
            socket.oscId = "Test";
            socket.addMenuItem = false;
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
#pragma warning disable 0612
            // Obsolete touch settings, including an explicit opposite choice, must be ignored.
            socket.length = 999;
            socket.unitsInMeters = false;
            socket.enableHandTouchZone2 = supportsTouch ? VRCFuryHapticSocket.EnableTouchZone.Off : VRCFuryHapticSocket.EnableTouchZone.On;
#pragma warning restore 0612
            injector.GetService<BakeHapticSocketsService>().Apply();
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => r.localOnly).ToArray();
            Assert.That(receivers, Has.Length.EqualTo(supportsTouch ? 2 : 0));
            Assert.That(fx.TryGetParam("OGB/Enabled", out _), Is.EqualTo(supportsTouch));
            Assert.That(injector.GetService<ExpressionParamsService>().Any(p => p.name == "OGB/Enabled"), Is.EqualTo(supportsTouch));
            if (!supportsTouch) return;
            foreach (var self in new[] { true, false }) {
                var receiver = receivers.Single(r => r.parameter == "OGB/Socket/Test/" + (self ? "TouchSelf" : "TouchOthers"));
                Assert.That(receiver.name, Is.EqualTo("Receiver - Touch (" + (self ? "Self" : "Others") + ")"));
                Assert.That(receiver.shapeType, Is.EqualTo(ContactBase.ShapeType.Sphere));
                Assert.That(receiver.receiverType, Is.EqualTo(ContactReceiver.ReceiverType.Proximity));
                Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderVelocity));
                Assert.That(fx.TryGetParam(receiver.parameter + "_SenderVelocityMagnitude", out _), Is.False);
                Assert.That(receiver.allowSelf, Is.EqualTo(self));
                Assert.That(receiver.allowOthers, Is.EqualTo(!self));
                Assert.That(receiver.collisionTags, Is.EquivalentTo(HapticUtils.SelfContacts));
                Assert.That(receiver.radius, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(receiver.position, Is.EqualTo(Vector3.zero));
                Assert.That(receiver.transform.parent.name, Is.EqualTo("WorldSpace"));
                Assert.That(receiver.gameObject.activeSelf, Is.False);
            }
            Start();
            playable.SetFloat("IsLocal", 1);
            playable.SetFloat("OGB/Enabled", 1);
            Evaluate(3);
            Assert.That(receivers.All(r => r.gameObject.activeSelf), Is.True);
            Assert.That(receivers.All(r => !playable.IsParameterControlledByCurve(r.parameter)), Is.True);
            playable.SetFloat("OGB/Enabled", 0);
            Evaluate(3);
            Assert.That(receivers.All(r => !r.gameObject.activeSelf), Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void SocketOgbDepthOnlyRunsRemotelyForPartiesUsedByDepthAnimations(bool animateSelf, bool animateOthers) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (!VF.Menu.HapticsToggleMenuItem.Get()) Assert.Ignore("OGB haptics are disabled");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.oscId = "Test";
            socket.addMenuItem = false;
            if (animateSelf || animateOthers) {
                socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                    units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                    enableSelf = animateSelf,
                    enableOthers = animateOthers
                });
            }
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { legacyModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            foreach (var self in new[] { true, false }) {
                var contact = contacts.Single(r => r.parameter == "OGB/Socket/Test/Depth" + (self ? "Self" : "Others"));
                Assert.That(contact.localOnly, Is.EqualTo(self ? !animateSelf : !animateOthers));
                Assert.That(contact.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.All));
            }
        }

        [Test]
        public void SocketBakeSharesDepthActionsAndInjectedDimensions() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.addMenuItem = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            socket.enablePlugLengthParameter = socket.enablePlugWidthParameter = true;
            socket.plugLengthParameterName = "InjectedLength";
            socket.plugWidthParameterName = "InjectedRadius";
            socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                units = VRCFuryHapticSocket.DepthActionUnits.Plugs, enableSelf = true,
                smoothingSeconds = 0.1f, reverseClip = true
            });
            var globals = injector.GetService<GlobalsService>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                sourceObject = root, sourceParam = VF.Inspector.VRCFuryHapticPlugEditor.SpsDepthMeters,
                resolvedParam = "InjectedDepth"
            });
            foreach (var source in new[] { SpsPlugEntryRadiusMeters, SpsPlugEntryRadiusLocal, SpsPlugEntryRadiusPlugLengths }) {
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = root, sourceParam = source, resolvedParam = "Injected" + source
                });
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var mainReceivers = root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Where(r => r.collisionTags.Contains(SpsTags.FormatPlug(r.allowSelf ? SelfTag("test") : "test"))).ToArray();
            Assert.That(mainReceivers, Has.Length.EqualTo(4));
            Assert.That(mainReceivers.Any(r => r.localOnly), Is.False);
            Assert.DoesNotThrow(() => injector.GetService<ParameterInjectService>().Validate());
            Start();
            foreach (var receiver in mainReceivers) {
                Set(receiver, 1, receiver.allowSelf ? 0.2f : 0.4f,
                    receiver.allowSelf ? 0.6f : 0.4f, receiver.allowSelf ? 0.06f : 0.04f, native: true);
            }
            Set(Merged(), 1, 0.2f, 0.6f, 0.06f, native: true);
            playable.SetFloat(Merged().parameter + "_SenderLossyScaleY", 0.03f);
            Evaluate(40);
            Assert.That(playable.GetFloat("InjectedLength"), Is.EqualTo(0.6f).Within(0.002f));
            Assert.That(playable.GetFloat("InjectedRadius"), Is.EqualTo(0.06f).Within(0.002f));
            Assert.That(playable.GetFloat("InjectedDepth"), Is.EqualTo(-0.4f).Within(0.002f));
            Assert.That(playable.GetFloat("Injected" + SpsPlugEntryRadiusMeters), Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(playable.GetFloat("Injected" + SpsPlugEntryRadiusLocal), Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(playable.GetFloat("Injected" + SpsPlugEntryRadiusPlugLengths), Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void SocketFramesKeepWorldDimensionsLocalUnitsAndIndependentHapticAndLegacySwitches() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (!VF.Menu.HapticsToggleMenuItem.Get()) Assert.Ignore("OGB haptics are disabled");
            injector.Set("autoChannelGenerator", new TouchBoneChannels(HumanBodyBones.Hips));
            root.transform.localScale = Vector3.one * 2;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = true }
            };
            globals.addOtherFeature = _ => { };
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.name = "Test";
            socket.addMenuItem = false;
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            socket.useRadiusOffset = true;
            var stop = GameObjects.Create("Guide", root);
            stop.localPosition = Vector3.forward;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
            var detector = injector.GetService<WorldScaleDetectorService>();
            foreach (var (source, output) in new[] { (SpsDepthMeters, "Depth meters"), (SpsDepthLocal, "Depth local"),
                         (SpsPlugRadiusLocal, "Radius local") }) {
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = root, sourceParam = source, resolvedParam = output
                });
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var bakeRoot = root.asVf().Children().Single(obj => obj.name == "BakedSpsSocket");
            var rawScale = detector.GetRawWorldScale(bakeRoot, "Socket scale");
            var uploadScale = bakeRoot.worldScale.x;
            var worldSpace = bakeRoot.Children().Single(obj => obj.name == "WorldSpace");
            var receiverSpace = bakeRoot.Children().Single(obj => obj.name == "ReceiverSpace");
            var legacyLights = bakeRoot.Children().Single(obj => obj.name == "Legacy Mode").Children().Single();
            Assert.That(bakeRoot.Children().Select(obj => obj.name), Is.EquivalentTo(new[] { "WorldSpace", "Legacy Mode", "ReceiverSpace" }));
            Assert.That(worldSpace.Children().All(obj => obj.Children().Length == 0), Is.True);
            Assert.That(bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Animations.ScaleConstraint>(), Has.Length.EqualTo(3));
            var contacts = bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>();
            Assert.That(contacts.All(r => r.transform.parent == (Transform)receiverSpace), Is.True);
            Assert.That(contacts.Select(r => r.name), Is.EquivalentTo(new[] {
                "Receiver - Plugs (Self)", "Receiver - Plugs (Others)",
                "Receiver - Plugs (Self Merged)", "Receiver - Plugs (Others Merged)",
                "Receiver - Legacy Plug Width (Others)", "Receiver - Touch (Self)", "Receiver - Touch (Others)"
            }));
            var haptics = contacts.Where(r => r.localOnly).ToArray();
            var lights = bakeRoot.GetComponentsInSelfAndChildren<Light>();
            Assert.That(haptics, Has.Length.EqualTo(2));
            Assert.That(haptics.All(r => !r.gameObject.activeSelf), Is.True);
            Assert.That(legacyLights.activeInHierarchy, Is.False);
            var legacy = injector.GetService<SpsOptionsService>().GetLegacyParameter();
            Start();
            foreach (var resize in new[] { 0.5f, 1f, 3f }) {
                root.transform.localScale = Vector3.one * resize;
                // Manual EditMode graph evaluation does not run scene constraints.
                worldSpace.worldScale = Vector3.one;
                receiverSpace.worldScale = Vector3.one;
                legacyLights.worldScale = Vector3.one;
                playable.SetFloat(rawScale, resize / (WorldScaleDetectorService.MaxDetectedScale * uploadScale));
                Set(Merged(), 1, 0.2f, 0.6f, 0.06f, native: true);
                Evaluate(12);
                Assert.That(Vector3.Distance(worldSpace.worldScale, Vector3.one), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(legacyLights.worldPosition, bakeRoot.worldPosition),
                    Is.EqualTo(VRCFuryHapticSocketEditor.LegacyRadiusOffset * resize).Within(0.0001f));
                Assert.That(Vector3.Distance(receiverSpace.worldPosition, legacyLights.worldPosition), Is.LessThan(.0001f));
                Assert.That(playable.GetFloat("Depth meters"), Is.EqualTo(-0.4f).Within(0.001f));
                Assert.That(playable.GetFloat("Depth local"), Is.EqualTo(-0.4f / resize).Within(0.001f));
                Assert.That(playable.GetFloat("Radius local"), Is.EqualTo(0.06f / resize).Within(0.001f));
                foreach (var receiver in contacts.Where(r => !r.localOnly)) {
                    Assert.That(receiver.radius * receiver.transform.lossyScale.x, Is.EqualTo(0.2f).Within(0.0001f));
                }
                var touch = haptics.Single(r => r.parameter.EndsWith("/TouchSelf"));
                Assert.That(touch.radius * touch.transform.lossyScale.x, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(Vector3.Distance(lights[0].transform.position, lights[1].transform.position), Is.EqualTo(0.01f).Within(0.0001f));
                foreach (var sender in bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()) {
                    Assert.That(sender.transform.lossyScale.y, Is.EqualTo(1).Within(0.0001f));
                    var expectedZ = sender.name == "Sender - Root" ? (int)SpsTags.SocketScaleFlagsZ.AnimatorHole | 1 : 1;
                    Assert.That(Mathf.RoundToInt(sender.transform.lossyScale.z), Is.EqualTo(expectedZ));
                }
            }
            playable.SetFloat("IsLocal", 1);
            playable.SetFloat("OGB/Enabled", 1);
            playable.SetFloat(legacy, 1);
            Evaluate(4);
            Assert.That(haptics.All(r => r.gameObject.activeSelf), Is.True);
            Assert.That(lights.All(l => l.gameObject.activeInHierarchy), Is.True);
            playable.SetFloat("OGB/Enabled", 0);
            playable.SetFloat(legacy, 0);
            Evaluate(4);
            Assert.That(haptics.All(r => !r.gameObject.activeSelf), Is.True);
            Assert.That(lights.All(l => !l.gameObject.activeInHierarchy), Is.True);
            Assert.That(Main().gameObject.activeSelf, Is.True);
            Assert.That(worldSpace.Children().Single(obj => obj.name == "Sender - Root").active, Is.True);
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false, true)]
        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        [TestCase(false, true, true)]
        public void SocketCalculationGateResetsOutputsForMenuAndParentToggles(bool useMenu, bool optimize, bool nonUniform = false) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var parent = GameObjects.Create("Socket parent", root);
            if (nonUniform) parent.localScale = new Vector3(1.2f, 0.7f, 1.8f);
            var obj = GameObjects.Create("Socket", parent);
            var socket = obj.AddComponent<VRCFuryHapticSocket>();
            socket.name = "Test";
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            socket.addMenuItem = useMenu;
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.enablePlugLengthParameter = socket.enablePlugWidthParameter = true;
            socket.plugLengthParameterName = "Injected length";
            socket.plugWidthParameterName = "Injected radius";
            var action = new VRCFuryHapticSocket.DepthActionNew {
                units = VRCFuryHapticSocket.DepthActionUnits.Meters, smoothingSeconds = 0.2f
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = "Action output", value = 1 });
            socket.depthActions2.Add(action);
            socket.activeActions.actions.Add(new VF.Model.StateAction.FxFloatAction { name = "Socket active output", value = 1 });
            fx.GetOrCreateFloat("Socket active output");
            var output = fx.GetOrCreateFloat("Action output");
            fx.GetOrCreateFloat("Injected depth", 77);
            injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                sourceObject = obj, sourceParam = SpsDepthMeters, resolvedParam = "Injected depth"
            });
            var parentEnabled = fx.CreateUniqueFloat("Parent enabled", 1);
            parentEnabled.MakeExpressionFloat(synced: false);
            var parentOn = VFClip.Create("Parent on"); parentOn.SetEnabled(parent, true);
            var parentOff = VFClip.Create("Parent off"); parentOff.SetEnabled(parent, false);
            fx.NewLayer("Parent toggle").NewState("Parent toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Parent toggle", parentEnabled, (0, parentOff), (1, parentOn)));
            injector.GetService<BakeHapticSocketsService>().Apply();
            GameObject relocatedRoot = null;
            if (nonUniform) {
                var group = root.transform.Find("Nonuniform SPS Sockets");
                Assert.That(group, Is.Not.Null);
                Assert.That(group.childCount, Is.EqualTo(1));
                relocatedRoot = group.GetChild(0).gameObject;
                Assert.That(socket.owner(), Is.EqualTo(obj), "Baking must leave the authored component in place");
            }
            var menuToggle = useMenu ? SocketMenuParameter("Test") : null;
            var internalValues = fx.parameters.Where(p => p.name.Contains("/Mapped") || p.name.Contains("/Smoothed")
                || p.name.Contains("/LegacyLengthValid") || p.name.Contains("/LegacyRadiusValid")).ToArray();
            Start();
            if (menuToggle != null) {
                playable.SetFloat(parentEnabled, 0);
                Evaluate(8);
                playable.SetFloat(menuToggle, 1);
                Evaluate(8);
                Assert.That(obj.activeInHierarchy, Is.False);
                Assert.That(playable.GetFloat("Socket active output"), Is.EqualTo(1),
                    "Menu actions must run even when the socket's parent is inactive");
                playable.SetFloat(menuToggle, 0);
                Evaluate(8);
                Assert.That(playable.GetFloat("Socket active output"), Is.Zero);
                playable.SetFloat(parentEnabled, 1);
            }
            if (menuToggle != null) playable.SetFloat(menuToggle, 1);
            void Contact(float proximity, float separation) {
                Set(Main(false), proximity, separation, 0.6f, 0.06f, native: true);
                Set(Merged(), proximity, separation, 0.6f, 0.06f, native: true);
            }
            Contact(1, 0.2f);
            Evaluate(120);
            if (nonUniform) Assert.That(relocatedRoot.activeSelf, Is.True);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.002f));
            if (useMenu) Assert.That(playable.GetFloat("Socket active output"), Is.EqualTo(1));
            Assert.That(playable.GetFloat("Injected depth"), Is.EqualTo(-0.4f).Within(0.002f));
            playable.SetFloat(useMenu ? menuToggle : parentEnabled, 0);
            // Initially-on objects need a frame to decode their activation delta before gating calculations.
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Evaluate(4);
            if (nonUniform) Assert.That(relocatedRoot.activeSelf, Is.False);
            if (useMenu) Assert.That(playable.GetFloat("Socket active output"), Is.Zero);
            Assert.That(playable.GetFloat("Injected depth"), Is.EqualTo(100).Within(0.001f));
            Assert.That(playable.GetFloat("Injected length"), Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(playable.GetFloat("Injected radius"), Is.EqualTo(0.03f).Within(0.001f));
            foreach (var value in internalValues) Assert.That(playable.GetFloat(value), Is.Zero.Within(0.001f));
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => !string.IsNullOrEmpty(r.parameter))) {
                Assert.That(playable.IsParameterControlledByCurve(receiver.parameter), Is.False, "SDK contact outputs must not become AAPs");
            }
            // Keep the manually supplied raw contact data stale while disabled, then clear it
            // before re-enabling. Neither calculations nor smoothing should retain penetration.
            Evaluate(20);
            Contact(0, -1);
            playable.SetFloat(parentEnabled, 1);
            if (menuToggle != null) playable.SetFloat(menuToggle, 1);
            Evaluate(120);
            if (nonUniform) {
                Assert.That(relocatedRoot.activeSelf, Is.True);
                playable.SetFloat(parentEnabled, 0);
                Evaluate(8);
                Assert.That(relocatedRoot.activeSelf, Is.False, "Parent activation must also gate sockets with a menu toggle");
                playable.SetFloat(parentEnabled, 1);
                Evaluate(8);
                Assert.That(relocatedRoot.activeSelf, Is.True);
            }
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Assert.That(playable.GetFloat("Injected depth"), Is.EqualTo(100).Within(0.001f));
        }

        private sealed class TouchBoneChannels : SpsAutoChannelGenerator {
            private readonly HumanBodyBones? bone;
            public TouchBoneChannels(HumanBodyBones? bone) => this.bone = bone;
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => bone;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones requested) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => new Vector3(0, 2, 0);
        }

        private sealed class AutoSelectionBones : SpsAutoChannelGenerator {
            public HumanBodyBones? GetClosestBone(VFGameObject obj) =>
                obj.name == "SocketB" ? HumanBodyBones.Chest : HumanBodyBones.Hips;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones requested) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => new Vector3(0, 1.8f, 0);
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

    internal class BakeHapticSocketsRadiusCapTest : SpsSenderBakeTestBase {
        [TestCase(0f)]
        [TestCase(float.Epsilon)]
        [TestCase(.0000001f)]
        [TestCase(.025f)]
        [TestCase(.5f)]
        [TestCase(float.PositiveInfinity)]
        public void AvatarSocketRadiusCapTracksScaleWithoutScalingTheMarker(float cap) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
            };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allFeaturesInRun = new List<VF.Model.Feature.FeatureModel> {
                new VF.Model.Feature.SpsOptions { legacyModeAvailable = false, autoModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var controllers = injector.GetService<ControllersService>();
            controllers.ClearCache(applyBaseMask: false);
            var fx = controllers.GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
            socket.addLight = Flags.Ring;
            socket.addMenuItem = false;
            socket.enableAuto = false;
            socket.useRadiusOffset = true;
            socket.maxRadiusOffset = cap;
            var guide = GameObjects.Create("Guide", root);
            guide.localScale = Vector3.one * 3;
            var nodeCap = cap * 2;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                transform = guide,
                useRadiusOffset = true,
                maxRadiusOffset = nodeCap
            });
            var sets = injector.GetService<SpsChannelsService>().Get(socket);
            sets.Request(new VF.Model.SpsChannelSettings {
                useSharedTag = false,
                channels = new List<VF.Model.SpsChannelSettings.Channel> { new() { id = "custom" } }
            });
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var bakeRoot = socket.owner().Children().Single(o => o.name == "BakedSpsSocket");
            var worldSpace = bakeRoot.Children().Single(o => o.name == "WorldSpace");
            var sender = worldSpace.Children().Single(o => o.name == "Sender - Root");
            var nodeSender = worldSpace.Children().Single(o => o.name == "Sender - Path Stop 1");
            var nodeRoot = guide.Children().Single(o => o.name == "BakedSpsPath");
            var packedWord = sender.worldScale.x;
            var nodePackedWord = nodeSender.worldScale.x;
            var uploadScale = bakeRoot.worldScale.x;
            var nodeUploadScale = nodeRoot.worldScale.x;
            var raw = cap > 0 && !float.IsPositiveInfinity(cap)
                ? injector.GetService<WorldScaleDetectorService>().GetRawWorldScale(bakeRoot, "Socket")
                : null;
            var nodeRaw = nodeCap > 0 && !float.IsPositiveInfinity(nodeCap)
                ? injector.GetService<WorldScaleDetectorService>().GetRawWorldScale(nodeRoot, "Node")
                : null;
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController {
                parameters = fx.parameters.Select(p => p.Save()).ToArray(),
                layers = fx.GetLayers().Select(l => l.Save(context)).ToArray()
            };
            var graph = PlayableGraph.Create("Socket radius cap");
            try {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                AnimationPlayableOutput.Create(graph, "Socket", animator).SetSourcePlayable(playable);
                graph.Play();
                foreach (var scale in new[] { .5f, 1.97f, 1.99f, 2f, 2.01f, 2.03f, 3f }) {
                    root.transform.localScale = Vector3.one * scale;
                    guide.localScale = Vector3.one * (1 + scale);
                    // Synchronous graph evaluation does not evaluate scene constraints.
                    worldSpace.worldScale = Vector3.one;
                    if (raw != null) playable.SetFloat(raw.name, scale / (WorldScaleDetectorService.MaxDetectedScale * uploadScale));
                    if (nodeRaw != null) playable.SetFloat(nodeRaw.name,
                        nodeRoot.worldScale.x / (WorldScaleDetectorService.MaxDetectedScale * nodeUploadScale));
                    for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                    var expected = cap == 0 || float.IsPositiveInfinity(cap) ? 1 : 2 + cap * scale;
                    Assert.That(sender.worldScale.y, Is.EqualTo(expected).Within(expected * .0001f));
                    var nodeExpected = nodeCap == 0 || float.IsPositiveInfinity(nodeCap) ? 1 : 2 + nodeCap * guide.worldScale.x;
                    Assert.That(nodeSender.worldScale.y, Is.EqualTo(nodeExpected).Within(nodeExpected * .0001f));
                    foreach (var variant in nodeSender.GetComponentsInSelfAndChildren<VRCContactSender>()) {
                        Assert.That(variant.transform.lossyScale.x, Is.EqualTo(nodePackedWord).Within(.01f));
                        Assert.That(variant.transform.lossyScale.y, Is.EqualTo(nodeExpected).Within(nodeExpected * .0001f));
                        Assert.That(variant.transform.lossyScale.z, Is.EqualTo(1).Within(.0001f));
                    }
                    var variants = sender.GetComponentsInSelfAndChildren<VRCContactSender>();
                    Assert.That(variants, Has.Length.EqualTo(2));
                    foreach (var variant in variants) {
                        Assert.That(variant.transform.lossyScale.x, Is.EqualTo(packedWord).Within(.01f));
                        Assert.That(variant.transform.lossyScale.y, Is.EqualTo(expected).Within(expected * .0001f));
                        Assert.That(variant.transform.lossyScale.z, Is.EqualTo(1).Within(.0001f));
                    }
                }
            } finally {
                graph.Destroy();
                Object.DestroyImmediate(controller);
                foreach (var asset in context.NewAssets) if (asset != null) Object.DestroyImmediate(asset);
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

    internal class BakeHapticSocketsExclusivityTest : SpsChannelBuildTestBase {
        [Test]
        public void SocketExclusivityDependsOnLegacyModeRatherThanChannels(
            [Values(false, true)] bool shared, [Values(false, true)] bool legacyPlugs,
            [Values(false, true)] bool tipLight) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var globals = injector.GetService<GlobalsService>();
            globals.addOtherFeature = _ => { };
            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            var actionController = new AnimatorController();
            assets.Add(actionController);
            var avatar = root.GetComponent<VRCAvatarDescriptor>();
            avatar.baseAnimationLayers = avatar.baseAnimationLayers.Append(new VRCAvatarDescriptor.CustomAnimLayer {
                type = VRCAvatarDescriptor.AnimLayerType.Action, animatorController = actionController
            }).ToArray();
            var options = new SpsOptions {
                autoModeAvailable = false, legacyModeAvailable = legacyPlugs, addDpsTipLight = tipLight
            };
            globals.allFeaturesInRun.Add(options);
            foreach (var name in new[] { "SocketA", "SocketB" }) {
                var socket = GameObjects.Create(name, root).AddComponent<VRCFuryHapticSocket>();
                socket.name = name;
                socket.addMenuItem = true;
                socket.channelSettings.useSharedTag = shared;
                socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = name });
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            var controllers = injector.GetService<ControllersService>();
            var layer = controllers.GetAction().GetLayers().SingleOrDefault(l => l.name.Contains("Socket Exclusivity"));
            if (!options.NeedsLegacyMode) {
                Assert.That(layer, Is.Null, "Without legacy mode, sockets remain independent");
                return;
            }
            Assert.That(layer, Is.Not.Null);
            Assert.That(layer.allStates.Select(s => s.name), Is.EquivalentTo(new[] { "Start", "SocketA", "SocketB" }),
                "Even private-channel sockets must be exclusive, before any channel-change requests are known");
            var legacy = injector.GetService<SpsOptionsService>().GetLegacyParameter();
            var menu = injector.GetService<MenuService>().GetMenu().GetSubmenu("SPS");
            var toggles = new[] { "SocketA", "SocketB" }.Select(name => menu.controls.Single(c => c.name == name).parameter).ToArray();
            for (var i = 0; i < toggles.Length; i++) {
                var state = layer.allStates.Single(s => s.name == (i == 0 ? "SocketA" : "SocketB"));
                var transition = layer.stateMachine.anyStateTransitions.Single(t => t.destinationState == state);
                Assert.That(transition.conditions.Select(c => c.parameter),
                    Is.EquivalentTo(new[] { toggles[i].name, legacy.name, "IsLocal" }));
                Assert.That(transition.conditions.All(c => c.mode == AnimatorConditionMode.If), Is.True,
                    "Exclusivity only runs locally with legacy mode and this socket turned on");
                var write = state.behaviours.GetBehaviours<VRCAvatarParameterDriver>().Single().parameters.Single();
                Assert.That(write.name, Is.EqualTo(toggles[1 - i].name));
                Assert.That(write.value, Is.Zero);
            }
        }
    }
}
