using static VF.Inspector.VRCFuryHapticPlugEditor;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
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
using VF.Hooks.GestureManagerFixes;
using VF.Hooks.Av3EmuFixes;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.Dynamics;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsDepthReceiverTest {
        private GameObject root;
        private VRCFuryInjector injector;

        private string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        private SelfAndOthersReceiverAnimatorService receiverAnimator;
        private VFAvatarController fx;
        private VFBlendTreeDirect tree;
        private BlendtreeMath math;
        private VFAParam scale;
        private Lazy<VFAParam> inverseScale;
        private Func<string, VFAParam> measurements;
        private bool optimizeTrees;
        private PlayableGraph graph;
        private AnimatorControllerPlayable playable;
        private readonly List<Object> assets = new();

        private sealed class TouchBoneChannels : SpsAutoChannelGenerator {
            private readonly HumanBodyBones? bone;
            public TouchBoneChannels(HumanBodyBones? bone) => this.bone = bone;
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => bone;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones requested) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => new Vector3(0, 2, 0);
        }

        [SetUp]
        public void SetUp() {
            optimizeTrees = false;
            root = new GameObject("Native depth tests");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel>();
            var controllers = injector.GetService<ControllersService>();
            controllers.ClearCache(applyBaseMask: false);
            fx = controllers.GetFx();
            receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            scale = fx.GetOrCreateFloat("ReferenceScale", 1);
            tree = injector.GetService<ObjectTreeService>().Get(root);
            math = new BlendtreeMath(fx, tree);
            inverseScale = new Lazy<VFAParam>(() => math.Invert("Inverse reference scale", scale));
        }

        [TearDown]
        public void TearDown() {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(root);
            foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private Func<Party, Func<string, VFAParam>> Socket(bool legacy = true) {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = legacy;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var service = injector.GetService<SpsSocketMeasurementsService>();
            var contacts = service.GetReceivers(socket, root, "Test");
            var detector = injector.GetService<WorldScaleDetectorService>();
            ((Dictionary<VFGameObject, (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector))
                [root] = (null, new Lazy<VFAParam>(() => scale), inverseScale);
            return service.GetMeasurements(socket, contacts, "Test");
        }

        private Func<Party, Func<string, VFAParam>> PlugMeasurements(VRCFuryHapticPlug plug) {
            var bakeRoot = injector.GetService<PlugFrameService>().Get(plug);
            var bake = new VRCFuryHapticPlugEditor.BakeResult {
                channelContacts = injector.GetService<SpsChannelsService>().Get(plug),
                bakeRoot = bakeRoot,
                socketReceivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug),
                localLength = 0.4f, localRadius = 0.05f
            };
            var detector = injector.GetService<WorldScaleDetectorService>();
            ((Dictionary<VFGameObject, (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector))
                [bakeRoot] = (null, new Lazy<VFAParam>(() => scale), inverseScale);
            return injector.GetService<SpsPlugMeasurementsService>().GetMeasurements(bake, "Plug");
        }

        private VRCContactReceiver Main(bool self = true) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.allowSelf == self && !r.name.Contains("Merged") && r.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug(""))));

        private VRCContactReceiver Width(bool self = true) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.allowSelf == self && r.collisionTags.Contains(SpsTags.LegacyPlugWidth));

        private VRCContactReceiver Merged() => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .First(r => r.name.Contains("Merged"));

        private void Start() {
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var counts = new Dictionary<VFMotion, long>();
            long CountLeaves(VFMotion motion) {
                if (motion == null) return 0;
                if (counts.TryGetValue(motion, out var count)) return count;
                return counts[motion] = motion is VFTree blend ? blend.children.Sum(child => CountLeaves(child.motion)) : 1;
            }
            Assert.That(fx.GetLayers().Sum(layer => layer.allStates.Sum(state => CountLeaves(state.motion))),
                Is.LessThan(50000), "Shared measurements must not expand into an impractically large runtime tree");
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            injector.GetService<NoBadControllerParamsService>().Apply();
            injector.GetService<FixPartiallyWeightedAapsService>().Apply();
            if (optimizeTrees) {
                injector.GetService<LayerToTreeService>().Apply();
                injector.GetService<TreeFlatteningService>().Optimize();
                injector.GetService<NoBadControllerParamsService>().Apply();
            }
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller); assets.AddRange(context.NewAssets);
            var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Native depth");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimatorControllerPlayable.Create(graph, controller);
            AnimationPlayableOutput.Create(graph, "Depth", animator).SetSourcePlayable(playable);
            graph.Play();
        }

        private void Set(VRCContactReceiver receiver, float proximity, float distance, float length = 1,
            float radius = 1, bool native = false, float x = 0, float y = 0, float z = 0) {
            void Field(string suffix, float value) {
                if (fx.TryGetParam(receiver.parameter + suffix, out _)) playable.SetFloat(receiver.parameter + suffix, value);
            }
            playable.SetFloat(receiver.parameter, proximity);
            Field("_PointSeparation", distance);
            Field("_SenderLossyScaleX", radius);
            Field("_SenderLossyScaleY", radius);
            Field("_SenderLossyScaleZ", length);
            Field("_SenderLocalPositionX", x);
            Field("_SenderLocalPositionY", y);
            Field("_SenderLocalPositionZ", z);
            foreach (var tag in receiver.collisionTags.Where(t => t.StartsWith(SpsTags.Prefix))) {
                Field("_CollisionTag_" + tag, native ? 1 : 0);
            }
            Field("_CollisionTag_" + SpsTags.LegacyPlugSphere, !native && proximity > 0 ? 1 : 0);
        }

        private void Evaluate(int frames = 12) {
            for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
        }

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
            Assert.That(flagTrees.All(t => t.children.All(child => child.motion is VFClip)), Is.True,
                "Each native socket flag must decode directly to clips in a single 1D tree");
            Start();
            var receiver = party == Party.Merged ? Merged() : root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Single(r => r.allowSelf == (party == Party.Self));
            foreach (var packed in new[] { 1, 8191, 8192, 16383, 16384, 24575, 24576, 32767 }) {
                Set(receiver, 0.9f, 0.2f, radius: packed, native: true);
                Evaluate();
                Assert.That(playable.GetFloat(hole), Is.EqualTo((packed & (int)HapticUtils.SocketScaleFlags.Collapse) != 0 ? 1 : 0));
                Assert.That(playable.GetFloat(isLegacy), Is.Zero);
            }
            if (shared && party != Party.Self) {
                foreach (var (holeTag, ringTag) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) {
                    Set(receiver, 0.9f, 0.2f, radius: 32767, native: false);
                    playable.SetFloat(receiver.parameter + "_CollisionTag_" + SpsTags.LegacySocketHole, holeTag);
                    playable.SetFloat(receiver.parameter + "_CollisionTag_" + SpsTags.LegacySocketRing, ringTag);
                    Evaluate();
                    Assert.That(playable.GetFloat(hole), Is.EqualTo(holeTag == 1 && ringTag == 0 ? 1 : 0));
                    Assert.That(playable.GetFloat(isLegacy), Is.EqualTo(1));
                }
            }
            Set(receiver, 0, -1, radius: 32767, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(hole), Is.Zero);
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(2, true, true)]
        [TestCase(1, false, false)]
        public void SocketFlagsAreConstantsMatchingEntranceType(int type, bool hole, bool? shrink = null) {
            var factory = Socket(false);
            var socket = root.GetComponent<VRCFuryHapticSocket>();
            socket.addLight = (VRCFuryHapticSocket.AddLight)type;
            if (shrink != null) socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { shrink = shrink.Value });
            foreach (var party in new[] { Party.Self, Party.Others, Party.Merged }) {
                Assert.That(factory(party)(SpsSocketIsHole).GetDefault(), Is.EqualTo(hole ? 1 : 0));
            }
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

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
        public void PrivateOnlyMeasurementsRemoveNativeComparisonAndLegacyCacheBranches() {
            optimizeTrees = true;
            var outputs = Socket(false)(Party.Others);
            var depth = outputs(SpsDepthLocal);
            var radius = outputs(SpsPlugRadiusLocal);
            _ = outputs(SpsPlugEntryRadiusLocal);
            new BlendtreeMath(fx, receiverAnimator.GetTree(
                injector.GetService<SpsSocketMeasurementsService>().GetReceivers(
                    root.GetComponent<VRCFuryHapticSocket>(), root, "Test")))
                .CopyInPlace(depth, "Injected depth");
            Start();
            Assert.That(new AnimatorIterator.Trees().From(fx)
                .Any(t => t.BlendParameter?.EndsWith("/Native") == true), Is.False);
            Assert.That(new AnimatorIterator.Trees().From(fx)
                .Any(t => t.BlendParameter?.EndsWith("/LegacyLengthValid") == true || t.BlendParameter?.EndsWith("/LegacyRadiusValid") == true
                    || t.BlendParameter?.EndsWith("/LegacyLengthSample") == true || t.BlendParameter?.EndsWith("/LegacyRadiusSample") == true), Is.False);
            Set(Main(false), 1, 0.2f, length: 0.4f, radius: 0.05f);
            Evaluate();
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.2f).Within(0.001f));
            Assert.That(playable.GetFloat("Injected depth"), Is.EqualTo(-0.2f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
        }

        [Test]
        public void DepthOnlyConsumersActivateTheOverlapWorkaround() {
            _ = Socket(false)(Party.Self)(SpsDepthMeters);
            Assert.That(fx.parameters.Any(p => p.name.StartsWith("Test/Self/Length")), Is.False,
                "Native depth must read sender Z directly without generating an unused length output");
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<OverlappingContactsFixService>().Fix();
            Assert.That(new AnimatorIterator.Clips().From(fx).Any(clip => clip.GetAllBindings()
                .Any(binding => binding.type == typeof(VRCContactReceiver) && binding.propertyName == "m_Enabled")), Is.True);
        }

        [Test]
        public void InteractingWithOthersUsesSelectedContact(
            [Values(true, false)] bool socket, [Values(0, 1, 2)] int partyIndex,
            [Values(true, false)] bool measurementsFirst) {
            var party = (Party)partyIndex;
            Func<Party, Func<string, VFAParam>> factory;
            if (socket) {
                factory = Socket();
            } else {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.05f;
                factory = PlugMeasurements(plug);
            }
            var measurements = factory(party);
            if (measurementsFirst) _ = measurements(SpsDepthMeters);
            var output = measurements(SpsInteractingWithOthers);
            Assert.That(measurements(SpsInteractingWithOthers), Is.SameAs(output));
            _ = factory(Party.Self)(SpsDepthMeters);
            _ = factory(Party.Others)(SpsDepthMeters);
            Start();
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            var self = contacts.Single(r => r.allowSelf && !r.name.Contains("Merged"));
            var others = contacts.Single(r => r.allowOthers && !r.name.Contains("Merged"));
            Assert.That(fx.parameters.Any(p => p.name.EndsWith("_SenderType")), Is.EqualTo(party == Party.Merged));
            foreach (var (selfDistance, othersDistance, proximity) in new[] {
                         (-1f, -1f, 0f), (-1f, 0f, 1f), (0f, -1f, 0f),
                         (0.1f, 0.2f, 1f), (0.2f, 0.1f, 1f), (0.1f, 0.1f, 1f),
                         (-1f, 0.1f, 0f), (-1f, -1f, 1f) }) {
                Set(self, selfDistance >= 0 ? 1 : 0, selfDistance, native: true);
                Set(others, proximity, othersDistance, native: true);
                if (party == Party.Merged) {
                    var selectSelf = selfDistance >= 0 && (othersDistance < 0 || selfDistance < othersDistance);
                    var merged = Merged();
                    Set(merged, selectSelf ? 1 : proximity, selectSelf ? selfDistance : othersDistance, native: true);
                    playable.SetFloat(merged.parameter + "_SenderType", selectSelf ? 1 : othersDistance < 0 ? 0 : othersDistance == 0 ? 3 : 2);
                }
                Evaluate();
                var expected = party != Party.Self && proximity > 0 && othersDistance >= 0
                    && (party == Party.Others || selfDistance < 0 || othersDistance <= selfDistance);
                Assert.That(playable.GetFloat(output), Is.EqualTo(expected ? 1 : 0).Within(0.000001f),
                    $"{party}: self={selfDistance}, others={othersDistance}, proximity={proximity}");
            }
        }

        [Test]
        public void InteractingWithLegacyTracksPresenceAndNativeTags(
            [Values(true, false)] bool socket, [Values(true, false)] bool shared,
            [Values(0, 1, 2)] int partyIndex) {
            var party = (Party)partyIndex;
            Func<Party, Func<string, VFAParam>> factory;
            if (socket) {
                factory = Socket(shared);
            } else {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.05f;
                plug.channelSettings.useSharedTag = shared;
                plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test", allowSelf = true, allowOthers = true });
                factory = PlugMeasurements(plug);
            }
            var measurements = factory(party);
            var output = measurements(SpsInteractingWithLegacy);
            Assert.That(measurements(SpsInteractingWithLegacy), Is.SameAs(output));
            _ = measurements(SpsDepthMeters);
            Start();
            var receiver = party == Party.Merged ? Merged() : root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Single(r => r.allowSelf == (party == Party.Self));
            Set(receiver, 0, -1);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero, "No contact");
            Set(receiver, 0.8f, 0.2f, native: false);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(shared && party != Party.Self ? 1 : 0), "Legacy remote contact");
            Set(receiver, 0.8f, 0.2f, native: true);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero, "Native tags take precedence, even when legacy tags also exist");
            Set(receiver, 0.8f, -1, native: false);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero, "Invalid separation must not count as a contact");
            Set(receiver, 0, 0.2f, native: false);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero, "Stale advanced outputs must not count as a contact");
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
                Assert.That(receiver.radius * receiver.transform.lossyScale.x, Is.EqualTo(1).Within(0.0001f));
            }
        }

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
                    Is.EqualTo(available ? 1 : 0.2f).Within(0.0001f));
                if (receiver.collisionTags.Contains(SpsTags.FormatPlug("test"))) {
                    Assert.That(receiver.collisionTags.Contains(SpsTags.LegacyPlugSphere),
                        Is.EqualTo(available && receiver.allowOthers));
                }
            }
            Assert.That(contacts.Any(r => r.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.EqualTo(available));
            Assert.That(fx.parameters.Any(p => p.name.Contains("Test/Others/Legacy")), Is.EqualTo(available));
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
        [TestCase(0f, 0.4f, 0.05f)]
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
            Set(Main(), 0, 0);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(100).Within(0.0001f));
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

        [Test]
        public void PlugDepthUsesItsOwnResizedDimensionsAndNeverDecodesSocketScaleAsLength() {
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
            Set(receiver, 0, -1);
            Evaluate();
            Assert.That(playable.GetFloat(outputs[2]), Is.EqualTo(100).Within(0.001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PlugDepthReceivesTpsSocketRootOnlyOnPublicRemoteRoute(bool shared) {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            plug.channelSettings.useSharedTag = shared;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var factory = PlugMeasurements(plug);
            var depth = factory(Party.Others)(SpsDepthMeters);
            _ = factory(Party.Self)(SpsDepthMeters);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true);
            var others = receivers.Single(r => r.allowOthers);
            var self = receivers.Single(r => r.allowSelf);
            var socket = new GameObject("Remote TPS socket");
            assets.Add(socket);
            var sender = socket.AddComponent<VRCContactSender>();
            sender.radius = 0.01f;
            sender.collisionTags = new List<string> { SpsTags.LegacySocket };
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var matches = sender.collisionTags.Intersect(others.collisionTags).Any();
            Assert.That(matches, Is.EqualTo(shared), "TPS sockets publish the root tag without SPS channel or hole/ring tags");
            Assert.That(sender.collisionTags.Intersect(self.collisionTags), Is.Empty,
                "Self contacts continue to require native SPS channels");
            Start();
            foreach (var distance in new[] { 0.5f, 0.2f, 0f }) {
                Set(others, matches ? 0.5f : 0, matches ? distance : -1, native: false);
                Evaluate();
                Assert.That(playable.GetFloat(depth), Is.EqualTo(shared ? distance - 0.4f : 100).Within(0.001f));
            }
        }

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true)]
        [TestCase(false)]
        public void PlugDepthAndDeformationUseTheSameEntranceReceivers(bool materialsFirst) {
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var renderer = root.AddComponent<MeshRenderer>();
            var visual = injector.GetService<SpsPlugReceiverService>();
            visual.GetReceivers(plug);
            if (materialsFirst) visual.Create(plug, new[] { renderer });
            var factory = PlugMeasurements(plug);
            _ = factory(Party.Self)(SpsDepthMeters);
            _ = factory(Party.Others)(SpsDepthMeters);
            if (!materialsFirst) visual.Create(plug, new[] { renderer });
            var parameters = new[] { true, false }.ToDictionary(self => self,
                self => receiverAnimator.GetParameter(visual.GetReceivers(plug), self).name);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(contacts, Has.Length.EqualTo(3));
            foreach (var self in new[] { true, false }) {
                var contact = contacts.Single(r => r.separateShaderParameter == (self ? "SPS_Self" : "SPS_Others"));
                Assert.That(contact.writeToMaterials, Is.True);
                Assert.That(contact.shaderTargetRenderers, Is.EqualTo(new[] { renderer }));
                Assert.That(contact.parameter, Is.EqualTo(parameters[self]));
                Assert.That(contact.advancedParameters.HasFlag(VRC.Dynamics.ContactReceiver.AdvancedParameter.PointSeparation), Is.True);
            }
            Assert.That(contacts.Single(r => r.separateShaderParameter == "SPS_Path").parameter, Is.Empty);
        }
#endif

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
            Contact(0.63f, 0.97f, 0.85f);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "Before insertion");
            Contact(0.6f, 1, 0.88f);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "Tip touching entrance");
            Contact(0.585f, 1, 0.88f);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.03f).Within(0.001f), "2.5% insertion");
            Contact(0.57f, 1, 0.88f);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.06f).Within(0.001f), "5% insertion");
            Contact(0.3f, 1, 0.88f);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.06f).Within(0.001f), "Further insertion");
            var receiver = merged ? Merged() : Main(false);
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.04f);
            Evaluate(30);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.04f).Within(0.001f), "Native plugs use sampled radius even with a stale legacy helper");
            Contact(0.3f, 1, 0);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "A plug without a radius helper must not use the assumed radius");
            Contact(0, 0, 0);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void LegacyLengthUsesCenterDistanceResetsOnExitAndIgnoresTeleports() {
            measurements = Socket()(Party.Others);
            var length = measurements(SpsPlugLengthMeters);
            var depth = measurements(SpsDepthMeters);
            Assert.That(fx.parameters.Any(p => p.name.Contains("LegacySourceStable") || p.name.EndsWith("_b")), Is.False);
            Start();
            Set(Main(false), 0.4f, 1.2f, length: 20, radius: 20, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(0.6f).Within(0.001f));
            Set(Main(false), 1, 0.4f, length: 20, radius: 20, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.2f).Within(0.001f));
            Set(Main(false), 0, 0);
            Evaluate();
            Set(Main(false), 1, 0.1f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.1f).Within(0.001f));
            Set(Main(false), 0.4f, 1.2f, z: 1.2f);
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
            var native = fx.parameters.Single(p => p.name.EndsWith(Main(false).parameter + "/Native"));
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
                    if (proximity <= 0 || separation < 0) { value = assumed; known = false; }
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
            var samples = new (float distance, float helper)[] {
                (0.8f, 0.68f), (1f, 0.72f), (4f, 0.68f),
                (0.1f, 0.68f), (0.8f, 0.9f), (0.8f, 0.21f), (0.8f, 0f)
            };
            for (var frame = 0; frame < 70; frame++) {
                var sample = samples[frame % samples.Length];
                Set(Main(false), 0.8f, sample.distance);
                Set(Width(false), sample.helper, sample.distance);
                var speed = frame % 2 == 0 ? -0.4f : 0.7f;
                playable.SetFloat(Main(false).parameter + "_SenderVelocityZ", speed);
                Evaluate(1);
                Assert.That(new[] { 0.3f, 0.6f, 0.8f }.Any(v => Mathf.Abs(v - playable.GetFloat(length)) < 0.001f), Is.True,
                    "A stale validity decision must not accept a negative or oversized length");
                Assert.That(new[] { 0.03f, 0.04f, 0.06f }.Any(v => Mathf.Abs(v - playable.GetFloat(radius)) < 0.001f), Is.True,
                    "A stale validity decision must not accept a negative radius or the helper's minimum-radius artifact");
                Assert.That(playable.GetFloat(depth), Is.EqualTo(0.2f).Within(0.001f));
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
            Contact(0.58f, 1, 0.9f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.58f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
            Contact(0.48f, 1, 0.99f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.58f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(-0.1f).Within(0.001f));
            Contact(0.62f, 0.98f, 0.86f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Contact(0.5f, 1, 0.98f);
            Assert.That(playable.GetFloat(length), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
        }

        [Test]
        public void OneMeterReceiverCanMeasureALongerLegacyPlug() {
            measurements = Socket()(Party.Others);
            var length = measurements(SpsPlugLengthMeters);
            var depth = measurements(SpsDepthMeters);
            Start();
            Set(Main(false), 0.6f, 1.6f, z: 1.6f);
            Evaluate();
            Assert.That(playable.GetFloat(length), Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(playable.GetFloat(depth), Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void LegacyWidthTrustsHelperRegardlessOfCenterButRejectsHelperFloor() {
            measurements = Socket()(Party.Others);
            var radius = measurements(SpsPlugRadiusMeters);
            Assert.That(fx.parameters.Any(p => p.name.Contains("LegacyWidthMatched") || p.name.Contains("Difference")), Is.False);
            Start();
            Set(Main(false), 0.8f, 0.8f, x: -0.4f, z: 1.2f);
            Set(Width(false), 0.68f, 0.8f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.68f, 0.8f, x: 0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.21f, 0.8f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.03f).Within(0.001f));
            Set(Width(false), 0.68f, 0.8f, x: -0.4f, z: 1.2f);
            Evaluate();
            Assert.That(playable.GetFloat(radius), Is.EqualTo(0.06f).Within(0.001f));
            Set(Width(false), 0.21f, 0.8f, x: -0.4f, z: 1.2f);
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

        [TestCase((int)Party.Self)]
        [TestCase((int)Party.Others)]
        [TestCase((int)Party.Merged)]
        public void AdvancedParameterRequestsSelectTheCorrectReceiversAndUnionFlags(int selection) {
            var party = (Party)selection;
            var channels = injector.GetService<SpsChannelsService>();
            var data = channels.Get(root.AddComponent<VRCFuryHapticSocket>());
            var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(new SelfAndOthersReceiverService.Request {
                frame = root, name = "Advanced", worldRadius = 3, getTags = (_, _) => new[] { "tag" }, channels = data
            });
            var velocity = receiverAnimator.GetAdvancedParam(receivers, party, "SenderVelocityZ");
            var parameter = receiverAnimator.GetParameter(receivers, party);
            Assert.That(velocity.name, Is.EqualTo(parameter.name + "_SenderVelocityZ"));
            var scale = receiverAnimator.GetAdvancedParam(receivers, party, "SenderLossyScaleX");
            Assert.That(scale.name, Is.EqualTo(parameter.name + "_SenderLossyScaleX"));
            var tag = receiverAnimator.GetAdvancedParam(receivers, party, "CollisionTag_tag");
            channels.Apply(data);
            var writers = root.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => r.parameter == parameter.name).ToArray();
            Assert.That(writers, Has.Length.EqualTo(party == Party.Merged ? 2 : 1));
            foreach (var writer in writers) {
                Assert.That(writer.advancedParameters.HasFlag(VRC.Dynamics.ContactReceiver.AdvancedParameter.SenderVelocity), Is.True);
                Assert.That(writer.advancedParameters.HasFlag(VRC.Dynamics.ContactReceiver.AdvancedParameter.SenderLossyScale), Is.True);
                Assert.That(writer.advancedParameters.HasFlag(VRC.Dynamics.ContactReceiver.AdvancedParameter.SenderLocalPosition), Is.False);
            }
            Assert.That(fx.TryGetParam(tag.name, out _), Is.True);
            Assert.That(writers.All(r => r.advancedParameters.HasFlag(VRC.Dynamics.ContactReceiver.AdvancedParameter.CollisionTags)), Is.True);
        }

        [Test]
        public void InvalidAdvancedParameterRequestsDoNotCreateReceivers() {
            var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(new SelfAndOthersReceiverService.Request {
                frame = root, name = "Advanced", worldRadius = 3
            });
            Assert.Throws<ArgumentOutOfRangeException>(() => receiverAnimator.GetAdvancedParam(receivers, (Party)999, "PointSeparation"));
            Assert.Throws<ArgumentException>(() => receiverAnimator.GetAdvancedParam(receivers, Party.Self, "Unknown"));
            Assert.That(receivers.IsCreated, Is.False);
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

        [TestCase(false)]
        [TestCase(true)]
        public void SdkAdvancedContactValuesMustUseThePlayableParameterBridge(bool av3Emu) {
            measurements = Socket()(Party.Self);
            var length = measurements(SpsPlugLengthMeters);
            var radius = measurements(SpsPlugRadiusMeters);
            var depth = measurements(SpsDepthMeters);
            var velocity = measurements(SpsVelocityMeters);
            Start();
            var receiver = Main();
            var managerObject = new GameObject("Advanced SDK bridge test");
            var previousManager = ContactManager.Inst;
            ContactManager manager = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo Field(Type type, string name) {
                for (; type != null; type = type.BaseType) {
                    var field = type.GetField(name, flags);
                    if (field != null) return field;
                }
                throw new Exception("Missing SDK field " + name);
            }
            object Call(object target, string name, params object[] args) {
                for (var type = target.GetType(); type != null; type = type.BaseType) {
                    var method = type.GetMethods(flags | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
                    if (method != null) return method.Invoke(target, args);
                }
                throw new Exception("Missing SDK method " + name);
            }
            try {
                manager = managerObject.AddComponent<ContactManager>();
                Field(typeof(ContactManager), "Inst").SetValue(null, manager);
                if (!(bool)Field(typeof(ContactManager), "hasInit").GetValue(manager)) Call(manager, "Init");
                receiver.playerId = 1;
                Field(receiver.GetType(), "_assignedUsage").SetValue(receiver, DynamicsUsage.Avatar);
                receiver.allowInit = true;
                Call(receiver, "Start");
                Func<string, IAnimParameterAccess> playableAccess = name => new TestParameterAccess(
                    () => playable.GetFloat(name), value => playable.SetFloat(name, value));
                receiver.paramAccess = playableAccess(receiver.parameter);
                var ordinaryAnimator = new Dictionary<string, float>();
                // Reproduce Gesture Manager replacing the main bridge while advanced SDK
                // accessors still write to a different Animator parameter store.
                receiver.RebindAdvancedParameters(name => new TestParameterAccess(
                    () => ordinaryAnimator.TryGetValue(name, out var value) ? value : 0,
                    value => ordinaryAnimator[name] = value));
                var senderObject = GameObjects.Create("Native plug", root);
                senderObject.localPosition = Vector3.forward * 0.8f;
                senderObject.localScale = new Vector3(0.04f, 0.04f, 0.4f);
                var sender = senderObject.AddComponent<VRCContactSender>();
                sender.radius = 1;
                sender.playerId = 1;
                sender.collisionTags = new List<string> { SpsTags.FormatPlug(SelfTag("test")) };
                Field(sender.GetType(), "_assignedUsage").SetValue(sender, DynamicsUsage.Avatar);
                sender.allowInit = true;
                Call(sender, "Start");
                void Tick() {
                    var schedule = typeof(ContactManager).GetMethod("ScheduleUpdateReceiversJob", flags);
                    var job = schedule.Invoke(manager, new[] { Activator.CreateInstance(schedule.GetParameters()[0].ParameterType) });
                    job.GetType().GetMethod("Complete").Invoke(job, null);
                    Call(Field(typeof(ContactManager), "collision").GetValue(manager), "Complete");
                    Call(manager, "HandleDynamicsFrameComplete");
                }
                Tick(); Tick();
                Evaluate();
                Assert.That(ordinaryAnimator[receiver.parameter + "_SenderLossyScaleZ"], Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(ordinaryAnimator[receiver.parameter + "_PointSeparation"], Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(playable.GetFloat(receiver.parameter + "_SenderLossyScaleZ"), Is.Zero);
                Assert.That(playable.GetFloat(length), Is.Zero);
                Assert.That(playable.GetFloat(radius), Is.Zero);

                if (av3Emu) {
                    receiver.paramAccess = new Av3EmuParameterAccess { runtime = playableAccess, paramName = receiver.parameter };
                    // A skipped remote contact must retain its existing advanced accessors.
                    var before = receiver.param_PointSeparation;
                    Av3EmuAdvancedContactsFixHook.Postfix(new object(), new[] { receiver });
                    Assert.That(receiver.param_PointSeparation, Is.SameAs(before));
                    Av3EmuAdvancedContactsFixHook.Postfix(playableAccess, new[] { receiver });
                } else {
                    receiver.paramAccess = new GestureParameterAccess(playableAccess, receiver.parameter);
                    FixGestureManagerAdvancedContactsHook.Postfix(playableAccess, receiver);
                }
                Tick(); Tick();
                receiver.param_SenderVelocityZ.floatVal = -0.6f;
                Evaluate();
                Assert.That(playable.GetFloat(length), Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(playable.GetFloat(radius), Is.EqualTo(0.04f).Within(0.001f));
                Assert.That(playable.GetFloat(depth), Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(playable.GetFloat(velocity), Is.EqualTo(-0.6f).Within(0.001f));
                foreach (var distance in new[] { 0.2f, 0.05f }) {
                    senderObject.localPosition = Vector3.forward * distance;
                    Tick(); Tick();
                    Evaluate();
                    Assert.That(playable.GetFloat(receiver.parameter), Is.EqualTo(1).Within(0.001f));
                    Assert.That(playable.GetFloat(depth), Is.EqualTo(distance - 0.4f).Within(0.001f));
                }
            } finally {
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObject);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
        }

        public sealed class Av3EmuParameterAccess : IAnimParameterAccess {
            public Func<string, IAnimParameterAccess> runtime;
            public string paramName;
            public float floatVal { get => runtime(paramName).floatVal; set => runtime(paramName).floatVal = value; }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
        }

        private sealed class TestParameterAccess : IAnimParameterAccess {
            private readonly Func<float> read;
            private readonly Action<float> write;
            public TestParameterAccess(Func<float> read, Action<float> write) { this.read = read; this.write = write; }
            public float floatVal { get => read(); set => write(value); }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObjectTreeCapturesLateDefaultsWithoutAnimatingSdkInputs(bool optimize) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = optimize;
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel>();
            var owner = GameObjects.Create("Owner", root);
            owner.localScale = Vector3.one * 2;
            var enabled = fx.CreateUniqueFloat("Owner enabled", 1);
            enabled.MakeExpressionFloat(synced: false);
            var objectTrees = injector.GetService<ObjectTreeService>();
            var calculations = objectTrees.Get(owner);
            var on = VFClip.Create("Owner on"); on.SetEnabled(owner, true);
            var off = VFClip.Create("Owner off"); off.SetEnabled(owner, false);
            fx.NewLayer("Owner toggle").NewState("Owner toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Owner toggle", enabled, (0, off), (1, on)));
            Assert.That(objectTrees.Get(owner), Is.SameAs(calculations));
            var output = fx.CreateUniqueFloat("Helper output", 4);
            calculations.Add(output.MakeSetter(9));
            var detector = injector.GetService<WorldScaleDetectorService>();
            var raw = detector.GetRawWorldScale(owner, "Owner");
            var scale = detector.GetWorldScale(owner, "Owner");
            var inverse = detector.GetInverseWorldScale(owner, "Owner");
            // Defaults are captured after helpers have contributed and resting values have changed.
            output.SetDefault(5);
            Start();
            playable.SetFloat(raw, 0.02f);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(9));
            Assert.That(playable.GetFloat(scale), Is.EqualTo(4).Within(0.001f));
            Assert.That(playable.GetFloat(inverse), Is.EqualTo(0.25f).Within(0.001f));
            playable.SetFloat(enabled, 0);
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.EqualTo(5));
            Assert.That(playable.GetFloat(scale), Is.EqualTo(4).Within(0.001f));
            Assert.That(playable.GetFloat(inverse), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(playable.IsParameterControlledByCurve(raw), Is.False);
        }

        [Test]
        public void ObjectTreeDefaultsUseRecordedMaterialValuesAndObjectReferences() {
            var renderer = GameObjects.Create("Default material", root).AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Color"));
            var replacement = new Material(material);
            assets.Add(material);
            assets.Add(replacement);
            renderer.sharedMaterial = material;
            const string property = "material._VRCFuryRecordedDefault";
            var resting = VFClip.Create("Recorded material default");
            resting.SetCurve(renderer, property, 0.4f);
            injector.GetService<AvatarBindingStateService>().ApplyClip(resting, "Test resting state");
            var active = VFClip.Create("Active material");
            active.SetCurve(renderer, property, 0.9f);
            active.SetCurve(renderer, "m_Materials.Array.data[0]", replacement);
            var objectTrees = injector.GetService<ObjectTreeService>();
            var calculations = objectTrees.Get(renderer.owner());
            calculations.Add(active);
            objectTrees.Apply();
            var disabled = new AnimatorIterator.Clips().From(fx).Single(c => c.name == "Default material - Disabled");
            var floatBinding = active.GetFloatBindings().Single();
            Assert.That(disabled.GetCurve(floatBinding, true).GetFirst().GetFloat(), Is.EqualTo(0.4f));
            var objectBinding = active.GetObjectBindings().Single();
            Assert.That(disabled.GetCurve(objectBinding, false).GetFirst().GetObject(), Is.SameAs(material));
        }

        [TestCase(0.25f, true)]
        [TestCase(1f, true)]
        [TestCase(3f, true)]
        [TestCase(0.25f, false)]
        [TestCase(3f, false)]
        public void ScaleDetectorSdkContactUsesFixedCalibrationWithOptionalOgbName(float uploadScale, bool reportLength) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            root.transform.localScale = Vector3.one * uploadScale;
            var detector = injector.GetService<WorldScaleDetectorService>();
            const float bakedLength = 0.4f;
            var rawParameter = detector.GetRawWorldScale(root, "Scale consumer", reportLength ? "OGB/Plug/Test/LengthMult1" : null);
            Assert.That(fx.parameters.Any(p => p.name.Contains("WSD - Scale consumer - Final")), Is.False);
            var inverse = detector.GetInverseWorldScale(root, "Inverse consumer");
            Assert.That(detector.GetInverseWorldScale(root, "Another inverse consumer"), Is.SameAs(inverse));
            Assert.That(fx.parameters.Any(p => p.name.Contains("WSD - Scale consumer - Final")), Is.False);
            var measuredScale = detector.GetWorldScale(root, "Scale consumer");
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single();
            Assert.That(receiver.parameter, Is.EqualTo(rawParameter.name));
            var sender = root.GetComponentsInChildren<VRCContactSender>(true).Single();
            var outer = receiver.owner().parent;
            var anchor = outer.parent;
            Assert.That(receiver.transform.localPosition.x, Is.EqualTo(1 / (100 * uploadScale)).Within(0.000001f));
            Assert.That(outer.GetComponent<ScaleConstraint>().GetSource(0).sourceTransform, Is.EqualTo(root.transform));
            Assert.That(detector.GetWorldScale(root, "Cached consumer"), Is.SameAs(measuredScale));
            if (reportLength) {
                Assert.That(receiver.parameter, Is.EqualTo("OGB/Plug/Test/LengthMult1"));
                Assert.That(detector.GetWorldScale(root, "OGB consumer", "OGB/Plug/Test/LengthMult1"), Is.SameAs(measuredScale));
            }
            injector.GetService<IsObjectEnabledService>().Get(root).SetDefault(true);
            Start();
            var managerObject = new GameObject("Scale contact manager");
            var previousManager = ContactManager.Inst;
            ContactManager manager = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo Field(Type type, string name) {
                for (; type != null; type = type.BaseType) {
                    var field = type.GetField(name, flags);
                    if (field != null) return field;
                }
                throw new Exception("Missing SDK field " + name);
            }
            object Call(object target, string name, params object[] args) {
                for (var type = target.GetType(); type != null; type = type.BaseType) {
                    var method = type.GetMethods(flags | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
                    if (method != null) return method.Invoke(target, args);
                }
                throw new Exception("Missing SDK method " + name);
            }
            try {
                manager = managerObject.AddComponent<ContactManager>();
                Field(typeof(ContactManager), "Inst").SetValue(null, manager);
                if (!(bool)Field(typeof(ContactManager), "hasInit").GetValue(manager)) Call(manager, "Init");
                outer.active = true;
                foreach (var contact in new ContactBase[] { sender, receiver }) {
                    contact.playerId = 1;
                    Field(contact.GetType(), "_assignedUsage").SetValue(contact, DynamicsUsage.Avatar);
                    contact.allowInit = true;
                    Call(contact, "Start");
                }
                var raw = 0f;
                receiver.paramAccess = new TestParameterAccess(() => raw, value => {
                    raw = value;
                    playable.SetFloat(receiver.parameter, value);
                });
                foreach (var resize in new[] { 1f, 0.25f, 2f, 10f }) {
                    root.transform.localScale = Vector3.one * (uploadScale * resize);
                    // Synchronous EditMode has no constraint stage. Apply the scale constraints'
                    // target scales, then let the SDK's actual shape and collision jobs run.
                    anchor.worldScale = Vector3.one;
                    // Exercise a distant anchor while keeping floating-point error deterministic.
                    anchor.worldPosition = new Vector3(40, 20, 0);
                    outer.worldScale = root.transform.lossyScale;
                    receiver.owner().worldScale = Vector3.one;
                    for (var i = 0; i < 3; i++) {
                        var schedule = typeof(ContactManager).GetMethod("ScheduleUpdateReceiversJob", flags);
                        var job = schedule.Invoke(manager, new[] { Activator.CreateInstance(schedule.GetParameters()[0].ParameterType) });
                        job.GetType().GetMethod("Complete").Invoke(job, null);
                        Call(Field(typeof(ContactManager), "collision").GetValue(manager), "Complete");
                        Call(manager, "HandleDynamicsFrameComplete");
                    }
                    Assert.That(raw * 100, Is.EqualTo(resize).Within(0.005f), "SDK contact output at resize " + resize);
                    if (reportLength) Assert.That(raw * (bakedLength * 100),
                        Is.EqualTo(bakedLength * resize).Within(0.002f), "OGB world length at resize " + resize);
                    Evaluate();
                    Assert.That(playable.GetFloat(measuredScale), Is.EqualTo(uploadScale * resize).Within(0.005f * uploadScale));
                    Assert.That(playable.GetFloat(inverse), Is.EqualTo(1 / (uploadScale * resize)).Within(0.005f / (uploadScale * resize)));
                }
            } finally {
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObject);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
        }

        // Gesture Manager's bridge constructor takes its module and parameter name.
        private sealed class GestureParameterAccess : IAnimParameterAccess {
            private readonly IAnimParameterAccess inner;
            public GestureParameterAccess(object module, string name) {
                inner = ((Func<string, IAnimParameterAccess>)module)(name);
            }
            public float floatVal { get => inner.floatVal; set => inner.floatVal = value; }
            public int intVal { get => inner.intVal; set => inner.intVal = value; }
            public bool boolVal { get => inner.boolVal; set => inner.boolVal = value; }
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
            Assert.That(fx.parameters.Any(p => p.name.EndsWith("_Excluded")), Is.True, "Disabling auto must preserve the manual menu toggle");
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).SingleOrDefault(r => r.name == "SpsAutoDistance");
            if (included == null) {
                Assert.That(receiver, Is.Null);
                Assert.That(fx.parameters.Any(p => p.name.EndsWith("_autoMode")), Is.False);
            } else {
                Assert.That(receiver, Is.Not.Null);
                var bakeRoot = included.owner().Children().Single(child => child.name == "BakedSpsSocket");
                Assert.That(receiver.owner().GetConstraints().Single(c => c.IsParent()).GetSources(), Is.EqualTo(new[] { bakeRoot }));
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AutoSelectionUsesCloseContactHysteresisAndLocksActiveSocket(bool remote, bool manualSocket) {
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
            injector.GetService<BakeHapticSocketsService>().Apply();
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.gameObject.name == "SpsAutoDistance");
            Assert.That(receiver.collisionTags, Is.EqualTo(new[] { SpsTags.PlugClose }));
            Assert.That(receiver.radius, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(receiver.allowOthers && receiver.allowSelf && receiver.localOnly, Is.True);
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.None));
            Assert.That(receiver.owner().GetConstraints().Single(c => c.IsParent()).GetSources(), Has.Length.EqualTo(2));
            Assert.That(fx.parameters.Any(p => p.name.EndsWith("_AutoActiveDist") || p.name.EndsWith("_AutoCloser")
                || p.name.EndsWith("_AutoSocketNum") || p.name == receiver.parameter + "_PointSeparation"), Is.False);
            var a = fx.parameters.Single(p => p.name.EndsWith("_SocketA"));
            var b = fx.parameters.Single(p => p.name.EndsWith("_SocketB"));
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

        [TestCase("socket", "__sps_unknown_meters")]
        [TestCase("socket", "__sps_depth_unknown")]
        [TestCase("plug", "__sps_unknown_meters")]
        [TestCase("plug", SpsPlugEntryRadiusMeters)]
        [TestCase("none", SpsDepthMeters)]
        public void UnsupportedParameterInjectionsFailBuild(string sourceType, string parameter) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (sourceType == "socket") root.AddComponent<VRCFuryHapticSocket>().addMenuItem = false;
            if (sourceType == "plug") {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.length = 0.4f;
                plug.radius = 0.05f;
            }
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            var injections = injector.GetService<ParameterInjectService>();
            injections.Register(new ParameterInjectService.Request {
                sourceObject = root, sourceParam = parameter, resolvedParam = "Injected output"
            });
            var error = Assert.Catch<Exception>(() => {
                if (sourceType == "plug") {
                    var baker = injector.GetService<BakeHapticPlugsService>();
                    baker.ApplyEarlyBake();
                    baker.Apply();
                } else {
                    injector.GetService<BakeHapticSocketsService>().Apply();
                }
                injections.Validate();
            });
            Assert.That(error.Message, Does.Contain(parameter).And.Contain("Injected output")
                .And.Contain(root.name).And.Contain("does not support"));
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
            scale.SetDefault(2);
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
            ((Dictionary<VFGameObject, (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector))
                [root] = (null, new Lazy<VFAParam>(() => scale), inverseScale);
            foreach (var (source, output) in new[] { (SpsDepthMeters, "Depth meters"), (SpsDepthLocal, "Depth local"),
                         (SpsPlugRadiusLocal, "Radius local") }) {
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = root, sourceParam = source, resolvedParam = output
                });
            }
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var bakeRoot = root.asVf().Children().Single(obj => obj.name == "BakedSpsSocket");
            var worldSpace = bakeRoot.Children().Single(obj => obj.name == "WorldSpace");
            var legacyLights = bakeRoot.Children().Single(obj => obj.name == "Legacy Mode").Children().Single();
            Assert.That(bakeRoot.Children().Select(obj => obj.name), Is.EquivalentTo(new[] { "WorldSpace", "Legacy Mode" }));
            Assert.That(worldSpace.Children().All(obj => obj.Children().Length == 0), Is.True);
            Assert.That(bakeRoot.GetComponentsInSelfAndChildren<UnityEngine.Animations.ScaleConstraint>(), Has.Length.EqualTo(2));
            var contacts = bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>();
            Assert.That(contacts.All(r => r.transform.parent == (Transform)worldSpace), Is.True);
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
                legacyLights.worldScale = Vector3.one;
                playable.SetFloat(scale, resize);
                Set(Merged(), 1, 0.2f, 0.6f, 0.06f, native: true);
                Evaluate(12);
                Assert.That(Vector3.Distance(worldSpace.worldScale, Vector3.one), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(legacyLights.worldPosition, bakeRoot.worldPosition),
                    Is.EqualTo(VRCFuryHapticSocketEditor.LegacyRadiusOffset * resize).Within(0.0001f));
                Assert.That(playable.GetFloat("Depth meters"), Is.EqualTo(-0.4f).Within(0.001f));
                Assert.That(playable.GetFloat("Depth local"), Is.EqualTo(-0.4f / resize).Within(0.001f));
                Assert.That(playable.GetFloat("Radius local"), Is.EqualTo(0.06f / resize).Within(0.001f));
                foreach (var receiver in contacts.Where(r => !r.localOnly)) {
                    Assert.That(receiver.radius * receiver.transform.lossyScale.x, Is.EqualTo(1).Within(0.0001f));
                }
                var touch = haptics.Single(r => r.parameter.EndsWith("/TouchSelf"));
                Assert.That(touch.radius * touch.transform.lossyScale.x, Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(Vector3.Distance(lights[0].transform.position, lights[1].transform.position), Is.EqualTo(0.01f).Within(0.0001f));
                foreach (var sender in bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()) {
                    Assert.That(sender.transform.lossyScale.y, Is.EqualTo(1).Within(0.0001f));
                    Assert.That(sender.transform.lossyScale.z, Is.EqualTo(1).Within(0.0001f));
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

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AvatarBuildAppliesLegacySocketCompatibilityIndependentlyOfLegacyMode(bool enabled, bool legacyAvailable) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { legacySocketCompatibility = enabled, legacyModeAvailable = legacyAvailable }
            };
            globals.addOtherFeature = _ => { };
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            var main = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            receiverAnimator.GetParameter(main, Party.Merged);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var senders = root.GetComponentsInChildren<VRCContactSender>(true);
            Assert.That(senders.Single(s => s.name == "Sender - Sphere").collisionTags.Contains(SpsTags.LegacyPlugSphere), Is.EqualTo(enabled));
            Assert.That(senders.Count(s => s.name == "Sender - Legacy Width" || s.name == "Sender - Legacy Root"), Is.EqualTo(enabled ? 2 : 0));
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true)) {
                foreach (var tag in new[] { SpsTags.LegacySocketHole, SpsTags.LegacySocketRing, SpsTags.LegacySocket }) {
                    Assert.That(receiver.collisionTags.Contains(tag), Is.EqualTo(enabled && receiver.allowOthers), receiver.name + ": " + tag);
                }
            }
        }

        [TestCase(false, false, true, true)]
        [TestCase(false, true, true, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, true, true)]
        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, true, true, false)]
        [TestCase(true, false, true, true, true)]
        [TestCase(true, true, true, true, true)]
        public void LegacyToggleControlsSocketLightsTpsFrontAndDpsTipIncludingPlugOnlyAvatars(bool withSocket, bool defaultOn, bool available, bool shared, bool legacyServiceFirst = false) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = withSocket, legacyModeAvailable = available,
                    legacyModeEnabledOnAvatarLoad = defaultOn, addDpsTipLight = available, tpsDepthAnimationCompatibility = available }
            };
            globals.addOtherFeature = _ => { };
            var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            plug.channelSettings.useSharedTag = shared;
            if (!shared) plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "private" });
            if (withSocket) {
                var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
                socket.name = "Socket";
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.addMenuItem = true;
                socket.channelSettings.useSharedTag = shared;
                if (!shared) socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "private" });
            }
            var serviceTypes = injector.GetServices<object>().Select(service => service.GetType()).ToArray();
            int SortPosition<T>() => Array.IndexOf(serviceTypes, typeof(T)) + 1;
            globals.currentMenuSortPosition = SortPosition<BakeHapticPlugsService>();
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            injector.GetService<BakeHapticPlugsService>().Apply();
            globals.currentMenuSortPosition = SortPosition<BakeHapticSocketsService>();
            injector.GetService<BakeHapticSocketsService>().Apply();
            var options = injector.GetService<SpsOptionsService>();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            globals.currentMenuSortPosition = legacyServiceFirst ? 0 : SortPosition<SpsOptionsService>();
            options.MoveMenus();
            var menus = injector.GetService<MenuService>();
            if (menus.GetReadOnlyMenu() != null) menus.GetMenu().SortMenu();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var legacyObjects = root.transform.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "LegacyLights" || t.name == "Light - Legacy DPS Trigger" || t.name == "Sender - Legacy Front").ToArray();
            Assert.That(legacyObjects, Has.Length.EqualTo(!available || !shared ? 0 : withSocket ? 3 : 1));
            if (!shared) return;
            var legacy = options.GetLegacyParameter();
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu();
            if (!available) {
                Assert.That(legacy, Is.Null);
                Assert.That(menu == null || menu.controls.Single(c => c.name == "SPS").subMenu.controls
                    .All(c => !c.name.Contains("Legacy Support")), Is.True);
                return;
            }
            var controls = menu.controls.Single(c => c.name == "SPS").subMenu.controls;
            Assert.That(controls.Count(c => c.parameter?.name == legacy.name), Is.EqualTo(1));
            Assert.That(controls.Last().parameter.name, Is.EqualTo(legacy.name), "Legacy is created after the socket menu items");
            if (withSocket) Assert.That(controls.First().parameter.name, Is.EqualTo(options.GetAutoParameter().name));
            Assert.That(legacyObjects.All(t => t.parent.name == "Legacy Mode" && !t.parent.gameObject.activeSelf), Is.True);
            Start();
            Evaluate(5);
            Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf == defaultOn), Is.True, "Legacy default on avatar load");
            foreach (var enabled in new[] { true, false, true, false }) {
                playable.SetFloat(legacy, enabled ? 1 : 0);
                Evaluate(3);
                Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf == enabled), Is.True);
                Assert.That(root.GetComponentsInChildren<VRCContactSender>(true)
                    .Where(sender => sender.name == "Sender - Root" || sender.name == "Sender - Sphere")
                    .All(sender => sender.gameObject.activeSelf), Is.True, "Main senders remain available in both legacy modes");
            }
        }

        [Test]
        public void CompatibilityFeaturesRespectDependenciesAndUseLegacyToggle(
            [Values(false, true)] bool legacyPlugs,
            [Values(false, true)] bool legacySockets,
            [Values(false, true)] bool tpsAnimations,
            [Values(false, true)] bool dpsAnimations) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions {
                    autoModeAvailable = false,
                    legacyModeAvailable = legacyPlugs,
                    legacySocketCompatibility = legacySockets,
                    tpsDepthAnimationCompatibility = tpsAnimations,
                    addDpsTipLight = dpsAnimations,
                    legacyModeEnabledOnAvatarLoad = true
                }
            };
            globals.addOtherFeature = _ => { };
            var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.addMenuItem = true;
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            injector.GetService<BakeHapticPlugsService>().Apply();
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.Any(t => t.name == "LegacyLights"), Is.EqualTo(legacyPlugs));
            Assert.That(transforms.Any(t => t.name == "Sender - Legacy Front"), Is.EqualTo(legacyPlugs && tpsAnimations));
            Assert.That(transforms.Any(t => t.name == "Light - Legacy DPS Trigger"), Is.EqualTo(legacySockets && dpsAnimations));
            var options = injector.GetService<SpsOptionsService>();
            var legacy = options.GetLegacyParameter();
            Assert.That(legacy != null, Is.EqualTo(legacyPlugs || (legacySockets && dpsAnimations)));
            options.MoveMenus();
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu();
            Assert.That(menu.controls.Single(c => c.name == "SPS").subMenu.controls
                .Count(c => c.name.Contains("Legacy Support")), Is.EqualTo(legacy != null ? 1 : 0));
            if (legacy == null) return;
            var legacyObjects = transforms.Where(t => t.name == "LegacyLights"
                || t.name == "Sender - Legacy Front" || t.name == "Light - Legacy DPS Trigger").ToArray();
            Start();
            Evaluate(5);
            Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf), Is.True);
            playable.SetFloat(legacy, 0);
            Evaluate(3);
            Assert.That(legacyObjects.All(t => !t.parent.gameObject.activeSelf), Is.True);
        }

        [Test]
        public void AllLocalHapticReceiversUseNativePlugAndSocketTags() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var previousHaptics = UnityEditor.EditorPrefs.GetBool("com.vrcfury.haptics", true);
            UnityEditor.EditorPrefs.SetBool("com.vrcfury.haptics", true);
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.name = "Socket";
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.addMenuItem = false;
                socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
                socket.enablePlugLengthParameter = true;
                socket.plugLengthParameterName = "InjectedLength";
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.name = "Plug";
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.04f;
                var touch = root.AddComponent<VRCFuryHapticTouchReceiver>();
                touch.name = "Touch";
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                var globals = injector.GetService<GlobalsService>();
                globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
                globals.allFeaturesInRun = new List<FeatureModel> {
                    new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
                };
                globals.addOtherFeature = _ => { };
                injector.GetService<BakeHapticSocketsService>().Apply();
                injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
                injector.GetService<BakeHapticPlugsService>().Apply();
                injector.GetService<VF.Feature.VRCFuryHapticTouchReceiverBuilder>().Apply();
                var plugReceivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
                var plugSelfParameter = receiverAnimator.GetParameter(plugReceivers, true);
                var plugOthersParameter = receiverAnimator.GetParameter(plugReceivers, false);
                injector.GetService<ChangeSpsChannelsService>().Apply();
                var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true);
                Assert.That(receivers.Where(r => r.allowSelf).SelectMany(r => r.collisionTags)
                    .Any(tag => (tag.StartsWith("TPS_") && tag != SpsTags.PlugClose) || tag.StartsWith("SPSLL_")), Is.False);
                foreach (var receiver in receivers.Where(r => r.localOnly)) {
                    Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderVelocity));
                    Assert.That(fx.TryGetParam(receiver.parameter, out _), Is.True);
                    foreach (var output in receiver.GetParameterNames().Skip(1)) {
                        Assert.That(fx.TryGetParam(output, out _), Is.False, output);
                    }
                }
                Assert.That(receivers.Single(r => r.parameter == "OGB/Touch/Touch/Self").collisionTags,
                    Does.Contain(SpsTags.PlugClose));
                var socketSelf = receivers.Single(r => r.parameter == "OGB/Socket/Socket/DepthSelf");
                var socketOthers = receivers.Single(r => r.parameter == "OGB/Socket/Socket/DepthOthers");
                var depthRadius = injector.GetService<ExpressionParamsService>()
                    .Single(p => p.name == "OGB/Socket/Socket/DepthRadius");
                Assert.That(depthRadius.valueType, Is.EqualTo(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float));
                Assert.That(depthRadius.defaultValue, Is.EqualTo(0.2f));
                Assert.That(depthRadius.IsNetworkSynced(), Is.False);
                Assert.That(depthRadius.saved, Is.False);
                Assert.That(socketSelf.radius * socketSelf.transform.lossyScale.x, Is.EqualTo(depthRadius.defaultValue));
                Assert.That(socketOthers.radius * socketOthers.transform.lossyScale.x, Is.EqualTo(depthRadius.defaultValue));
                Assert.That(receivers.Count(r => !r.name.Contains("Merged") && r.collisionTags.Contains(SpsTags.FormatPlug(r.allowSelf ? SelfTag("test") : "test"))), Is.EqualTo(2),
                    "Socket OGB and depth measurements must share the native receivers");
                var plugSelf = receivers.Single(r => r.parameter == "OGB/Plug/Plug/DepthSelf");
                var plugOthers = receivers.Single(r => r.parameter == "OGB/Plug/Plug/DepthOthers");
                var lengthReceiver = receivers.Single(r => r.parameter == "OGB/Plug/Plug/LengthMult1");
                var plugRoot = injector.GetService<PlugFrameService>().Get(plug);
                var plugContacts = plugRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>();
                Assert.That(plugContacts.All(r => r.transform.parent == (Transform)plugRoot), Is.True);
                var plugHaptics = plugContacts.Where(r => r.localOnly).ToArray();
                Assert.That(plugHaptics, Has.Length.EqualTo(3));
                Assert.That(plugHaptics.Any(r => r.parameter.EndsWith("/TouchSelfClose")
                    || r.parameter.EndsWith("/TouchOthersClose") || r.parameter.EndsWith("/FrotOthersClose")), Is.False);
                foreach (var self in new[] { true, false }) {
                    var touchReceiver = plugHaptics.Single(r => r.parameter == "OGB/Plug/Plug/" + (self ? "TouchSelf" : "TouchOthers"));
                    Assert.That(touchReceiver.name, Is.EqualTo("Receiver - Touch (" + (self ? "Self" : "Others") + ")"));
                    Assert.That(touchReceiver.shapeType, Is.EqualTo(ContactBase.ShapeType.Box));
                    Assert.That(touchReceiver.useFaceProximity, Is.True);
                    Assert.That(touchReceiver.receiverType, Is.EqualTo(ContactReceiver.ReceiverType.Proximity));
                    Assert.That(touchReceiver.allowSelf, Is.EqualTo(self));
                    Assert.That(touchReceiver.allowOthers, Is.EqualTo(!self));
                    Assert.That(Vector3.Distance(touchReceiver.size, new Vector3(0.16f, 0.16f, 0.4f)), Is.LessThan(0.0001f));
                    var endDirection = touchReceiver.transform.TransformDirection(touchReceiver.rotation * Vector3.forward);
                    Assert.That(Vector3.Dot(endDirection, plugRoot.worldRotation * Vector3.forward), Is.EqualTo(-1).Within(0.0001f));
                }
                var frot = plugHaptics.Single(r => r.parameter == "OGB/Plug/Plug/FrotOthers");
                Assert.That(frot.name, Is.EqualTo("Receiver - Plugs (Others)"));
                Assert.That(frot.shapeType, Is.EqualTo(ContactBase.ShapeType.Box));
                Assert.That(frot.useFaceProximity, Is.True);
                Assert.That(frot.receiverType, Is.EqualTo(ContactReceiver.ReceiverType.Proximity));
                Assert.That(frot.allowSelf, Is.False);
                Assert.That(frot.allowOthers, Is.True);
                Assert.That(frot.collisionTags, Is.EqualTo(new[] { SpsTags.PlugClose }));
                Assert.That(Vector3.Distance(frot.size, new Vector3(0.24f, 0.24f, 0.4f)), Is.LessThan(0.0001f));
                Assert.That(frot.position, Is.EqualTo(plugHaptics.First(r => r.name == "Receiver - Touch (Others)").position));
                Assert.That(frot.rotation, Is.EqualTo(Quaternion.Euler(0, 180, 0)));
                Assert.That(plugHaptics.All(r => !r.gameObject.activeSelf), Is.True);
                Assert.That(plugSelf.gameObject.activeSelf && plugOthers.gameObject.activeSelf, Is.True);
                var scaleDetector = injector.GetService<WorldScaleDetectorService>();
                var measuredScale = scaleDetector.GetWorldScale(plugRoot, "Another consumer");
                Assert.That(receivers.Count(r => r.collisionTags.Contains("VRCF_WSD")
                    && r.transform.parent.GetComponent<ScaleConstraint>().GetSource(0).sourceTransform == plugRoot), Is.EqualTo(1));
                Assert.That(fx.parameters.Single(p => p.name == lengthReceiver.parameter).GetDefault(), Is.EqualTo(0.01f).Within(0.000001f));
                var lengthMult = injector.GetService<ExpressionParamsService>().Single(p => p.name == "OGB/Plug/Plug/LengthMult2");
                Assert.That(lengthMult.defaultValue, Is.EqualTo(40).Within(0.0001f));
                Assert.That(lengthMult.IsNetworkSynced(), Is.False);
                Assert.That(lengthMult.saved, Is.False);
                Assert.That(fx.parameters.Any(p => p.name == lengthMult.name), Is.False);
                Assert.That(plugSelfParameter.name, Is.EqualTo(plugSelf.parameter));
                Assert.That(plugOthersParameter.name, Is.EqualTo(plugOthers.parameter));
                Assert.That(receivers.Any(r => r.parameter.EndsWith("NewRoot") || r.parameter.EndsWith("NewTip")
                    || r.parameter.EndsWith("/PenOthersClose")), Is.False);
                foreach (var receiver in new[] { socketSelf, socketOthers, plugSelf, plugOthers }) {
                    Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.All));
                    Assert.That(fx.TryGetParam(receiver.parameter + "_PointSeparation", out _), Is.True);
                    Assert.That(fx.TryGetParam(receiver.parameter + "_SenderLossyScaleZ", out _), Is.False,
                        "The length consumer reads the merged receiver, not these raw OGB outputs");
                    Assert.That(fx.TryGetParam(receiver.parameter + "_SenderVelocityZ", out _), Is.False);
                }
                Assert.That(plugSelf.collisionTags, Is.All.StartsWith(SpsTags.Prefix));
                Assert.That(plugSelf.radius * plugSelf.transform.lossyScale.x, Is.EqualTo(0.64f).Within(0.001f));
                Assert.That(new AnimatorIterator.Clips().From(fx).SelectMany(clip => clip.GetAllBindings())
                    .Any(binding => binding.type == typeof(Animator)
                        && (binding.propertyName.StartsWith("OGB/Plug/") || binding.propertyName.StartsWith("OGB/Socket/"))), Is.False);
                Start();
                playable.SetFloat("IsLocal", 1);
                playable.SetFloat("OGB/Enabled", 1);
                Evaluate(3);
                Assert.That(plugHaptics.All(r => r.gameObject.activeSelf), Is.True);
                playable.SetFloat("OGB/Enabled", 0);
                Evaluate(3);
                Assert.That(plugHaptics.All(r => !r.gameObject.activeSelf), Is.True);
                Assert.That(plugSelf.gameObject.activeSelf && plugOthers.gameObject.activeSelf, Is.True);
                Set(socketSelf, 0.8f, 0.6f, 0.4f, 0.04f, native: true);
                Set(plugSelf, 0.6875f, 0.2f);
                Evaluate();
                Assert.That(playable.GetFloat(socketSelf.parameter), Is.EqualTo(0.8f));
                Assert.That(playable.GetFloat(socketSelf.parameter + "_PointSeparation"), Is.EqualTo(0.6f));
                Assert.That(playable.GetFloat(plugSelf.parameter), Is.EqualTo(0.6875f));
                Assert.That(playable.GetFloat(plugSelf.parameter + "_PointSeparation"), Is.EqualTo(0.2f));
                Assert.That(playable.GetFloat(measuredScale), Is.EqualTo(1).Within(0.001f));
                playable.SetFloat(lengthReceiver.parameter, 0.02f);
                Evaluate();
                Assert.That(playable.GetFloat(lengthReceiver.parameter) * lengthMult.defaultValue, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(playable.GetFloat(measuredScale), Is.EqualTo(2).Within(0.001f));
                Set(socketSelf, 0, -1);
                Set(plugSelf, 0, -1);
                Evaluate();
                Assert.That(playable.GetFloat(socketSelf.parameter), Is.Zero);
                Assert.That(playable.GetFloat(plugSelf.parameter), Is.Zero);
            } finally {
                UnityEditor.EditorPrefs.SetBool("com.vrcfury.haptics", previousHaptics);
            }
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public void PlugBakeAfterResizingReportsLengthAndInjectsConstantDimensions(float resize) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (!VF.Menu.HapticsToggleMenuItem.Get()) Assert.Ignore("OGB haptics are disabled");
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.name = "Test";
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var constants = new[] {
                (SpsPlugLengthLocal, 0.4f), (SpsPlugRadiusLocal, 0.05f),
                (SpsPlugLengthPlugLengths, 1f), (SpsPlugRadiusPlugLengths, 0.125f)
            };
            foreach (var (source, _) in constants) {
                fx.GetOrCreateFloat("Injected" + source, 77);
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = root, sourceParam = source, resolvedParam = "Injected" + source
                });
            }
            var baker = injector.GetService<BakeHapticPlugsService>();
            baker.ApplyEarlyBake();
            root.transform.localScale = Vector3.one * resize;
            baker.Apply();
            var multiplier = injector.GetService<ExpressionParamsService>().Single(p => p.name == "OGB/Plug/Test/LengthMult2");
            Assert.That(multiplier.defaultValue, Is.EqualTo(40 * resize).Within(0.0001f));
            Assert.That(fx.parameters.Any(p => p.name.Contains("WSD - Test - Final")), Is.False,
                "Naming the public raw detector and requesting constant dimensions must not force decoded scale");
            Start();
            Evaluate();
            foreach (var (source, expected) in constants) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(expected).Within(0.0001f));
            }
            Assert.That(playable.GetFloat("OGB/Plug/Test/LengthMult1") * multiplier.defaultValue,
                Is.EqualTo(0.4f * resize).Within(0.0001f));
        }

        [TestCase(0f, false, false)]
        [TestCase(0.1f, false, false)]
        [TestCase(0.1f, false, true)]
        [TestCase(0f, true, false)]
        [TestCase(0f, true, true)]
        [TestCase(0.1f, true, false)]
        [TestCase(0f, true, false, true)]
        [TestCase(0.1f, true, true, true)]
        public void PlugEntryRadiusAnimationsUseSampledRadiusWithDepthAnimationOptions(float smoothingSeconds, bool motionTime, bool self, bool savedReverseClip = false) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addMenuItem = true;
            socket.name = "RadiusTest";
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var output = fx.GetOrCreateFloat("Radius action output");
            var action = new VRCFuryHapticSocket.PlugEntryRadiusAction {
                range = new Vector2(0, 0.1f), smoothingSeconds = smoothingSeconds, enableSelf = self, reverseClip = savedReverseClip
            };
            if (motionTime) {
                var clip = VFClip.Create("Radius motion time");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            socket.plugEntryRadiusActions.Add(action);
            injector.GetService<BakeHapticSocketsService>().Apply();
            var toggle = fx.parameters.Single(p => p.name.EndsWith("_RadiusTest"));
            Start();
            playable.SetFloat(toggle, 1);
            var receiver = self ? Merged() : Main(false);
            Set(receiver, 0.9f, 0.66f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Radius animations stay off beyond 5% outside");
            Set(receiver, 0.9f, 0.615f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.25f).Within(0.01f), "2.5% outside gives half the sampled radius");
            Set(receiver, 1, 0.6f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f), "Tip touching the entrance permits radius animations");
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f));
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.0005f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.005f).Within(0.0001f),
                "Radius animations must follow values below 1% of the animation range");
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.01f));
            Set(receiver, 0.9f, 0.66f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            if (smoothingSeconds > 0) {
                Evaluate(4);
                Assert.That(playable.GetFloat(output), Is.GreaterThan(0.01f), "Withdrawing must let smoothing fade the animation out");
            }
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Withdrawing returns the animation to zero");
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            Evaluate(120);
            playable.SetFloat(toggle, 0);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Disabled sockets must stop radius animations");
            playable.SetFloat(toggle, 1);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.01f));
            Set(receiver, 0, -1, length: 0.6f, radius: 0.2f, native: true);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f));
        }

        [TestCase("party")]
        [TestCase("shape")]
        public void AnimationWithNoAllowedInteractionsIsSkipped(string pair) {
            root.AddComponent<VRCFuryHapticPlug>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var action = new VRCFuryHapticSocket.DepthActionNew {
                enableSelf = pair != "party", enableOthers = pair != "party",
                allowHole = pair != "shape", allowRing = pair != "shape"
            };
            var parameters = fx.parameters.Length;
            var layers = fx.GetLayers().Count();
            Assert.DoesNotThrow(() => injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root,
                "Skipped", _ => throw new Exception("Skipped animations must not request measurements"), root));
            Assert.That(fx.parameters.Length, Is.EqualTo(parameters));
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
        }

        [Test]
        public void PlugDepthAnimationFiltersBeforeSmoothing(
            [Values(false, true)] bool motionTime, [Values(0f, 0.1f)] float seconds,
            [Values(false, true)] bool selfOnly, [Values(false, true)] bool holeAllowed,
            [Values(false, true)] bool reverse) {
            root.AddComponent<VRCFuryHapticPlug>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Filtered depth", -0.125f);
            var hole = fx.CreateUniqueFloat("Filtered hole");
            var output = fx.GetOrCreateFloat("Filtered output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                enableSelf = selfOnly, enableOthers = !selfOnly,
                allowHole = holeAllowed, allowRing = !holeAllowed,
                smoothingSeconds = seconds, reverseClip = reverse
            };
            if (motionTime) {
                var clip = VFClip.Create("Filtered motion");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Filtered", party => {
                Assert.That(party, Is.EqualTo(selfOnly ? Party.Self : Party.Others));
                return source => source == SpsSocketIsHole ? hole : depth;
            }, root);
            Start();
            foreach (var isHole in new[] { false, true }) {
                playable.SetFloat(hole, isHole ? 1 : 0);
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.EqualTo(isHole == holeAllowed ? 0.5f : 0).Within(0.001f));
            }
            if (seconds > 0 && !reverse) {
                playable.SetFloat(hole, holeAllowed ? 1 : 0);
                Evaluate(120);
                playable.SetFloat(hole, holeAllowed ? 0 : 1);
                Evaluate(3);
                Assert.That(playable.GetFloat(output), Is.GreaterThan(0.001f), "Filters feed smoothing instead of snapping the animation off");
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            }
        }

        [Test]
        public void SocketDepthAnimationWeightsByLocalPlugRadius(
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool optimize,
            [Values(0f, 0.1f)] float smoothingSeconds,
            [Values(0f, 0.05f)] float zeroRadius) {
            optimizeTrees = optimize;
            root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Raw depth", -0.125f);
            var radius = fx.CreateUniqueFloat("Local plug radius", (zeroRadius + 0.25f) / 2);
            var output = fx.GetOrCreateFloat("Weighted depth output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), plugRadiusForFullWeight = 0.25f,
                smoothingSeconds = smoothingSeconds
            };
            action.plugRadiusForZeroWeight = zeroRadius;
            if (motionTime) {
                var clip = VFClip.Create("Weighted motion time");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Weighted",
                _ => measurement => measurement == SpsPlugRadiusLocal ? radius : depth, root);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.25f).Within(0.01f), "Half depth times half radius weight");
            playable.SetFloat(radius, 0.5f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f), "Radius weight must cap at one");
            playable.SetFloat(radius, 0);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void ZeroPlugWidthLetsLowerLayersDriveTheDepthAction(
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool optimize,
            [Values(false, true)] bool reverse,
            [Values(0f, 0.05f)] float zeroRadius) {
            optimizeTrees = optimize;
            root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Raw depth", -0.0625f);
            var radius = fx.CreateUniqueFloat("Local plug radius");
            var output = fx.GetOrCreateFloat("Shared weighted output");
            fx.NewLayer("Lower output").NewState("Lower output").WithAnimation(output.MakeSetter(0.6f));
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), plugRadiusForFullWeight = 0.25f, reverseClip = reverse
            };
            var clip = VFClip.Create("Weighted depth action");
            action.plugRadiusForZeroWeight = zeroRadius;
            clip.SetAap(output, motionTime ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Shared weighted",
                _ => measurement => measurement == SpsPlugRadiusLocal ? radius : depth, root);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.6f).Within(0.001f));
            playable.SetFloat(radius, 0.125f);
            Evaluate(120);
            var radiusWeight = (0.125f - zeroRadius) / (0.25f - zeroRadius);
            Assert.That(playable.GetFloat(output), Is.EqualTo((motionTime && reverse ? 0.75f : 0.25f) * radiusWeight).Within(0.001f));
            foreach (var value in new[] { zeroRadius, 0, -0.125f }) {
                playable.SetFloat(radius, value);
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.EqualTo(0.6f).Within(0.001f), "Zero width must release the shared property");
            }
        }

        [Test]
        public void WidthMultiplierUsesSharedReceiversAndSocketLocalUnits(
            [Values(0, 1, 2)] int unitsIndex,
            [Values(0, 1, 2)] int partyIndex) {
            var units = (VRCFuryHapticSocket.DepthActionUnits)unitsIndex;
            var party = (Party)partyIndex;
            var measurements = Socket(legacy: false);
            _ = measurements(party)(SpsDepthMeters);
            var receiverCount = party == Party.Merged ? 4 : 1;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var output = fx.GetOrCreateFloat("Local width output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.5f, 0), units = units, plugRadiusForFullWeight = 0.2f,
                enableSelf = party != Party.Others, enableOthers = party != Party.Self
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Local width", measurements, root);
            Start();
            var receiver = party == Party.Merged ? Merged() : Main(party == Party.Self);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(receiverCount),
                "Width weighting must reuse the depth/deformation receivers");
            foreach (var resize in new[] { 0.5f, 1, 2 }) {
                root.transform.localScale = Vector3.one * resize;
                playable.SetFloat(scale, resize);
                Set(receiver, 1, 0.35f * resize, length: 0.6f * resize, radius: 0.1f * resize, native: true);
                Evaluate(120);
                var depthWeight = units == VRCFuryHapticSocket.DepthActionUnits.Local ? 0.5f
                    : units == VRCFuryHapticSocket.DepthActionUnits.Meters ? Mathf.Clamp01(0.5f * resize)
                    : 0.25f / 0.6f / 0.5f;
                Assert.That(playable.GetFloat(output), Is.EqualTo(depthWeight * 0.5f).Within(0.003f),
                    "Width weight must use local units independently of depth units, party, and live avatar scale");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WidthMultiplierIsIgnoredForPlugDepthAndSocketRadius(bool plug) {
            if (plug) root.AddComponent<VRCFuryHapticPlug>();
            else root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Input", plug ? -0.125f : 0.125f);
            var output = fx.GetOrCreateFloat("Output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = plug ? new Vector2(-0.25f, 0) : new Vector2(0, 0.25f), plugRadiusForFullWeight = 0.25f
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Unsupported width",
                _ => source => {
                    Assert.That(source, Is.Not.EqualTo(SpsPlugRadiusLocal), "Unsupported actions must not request width measurements");
                    return input;
                }, root, plug ? SpsParamDepth : SpsParamPlugEntryRadius);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.001f));
        }

        [TestCase(0f, false)]
        [TestCase(0.1f, false)]
        [TestCase(0f, true)]
        public void DepthActionsKeepTheirRangeAndOnlyMapWhenRequired(float smoothingSeconds, bool motionTime) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var output = fx.GetOrCreateFloat("Depth action output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = smoothingSeconds
            };
            if (motionTime) {
                var clip = VFClip.Create("MotionTime action");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test",
                _ => _ => input, root);
            var requiresMap = smoothingSeconds > 0 || motionTime;
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Mapped")), Is.EqualTo(motionTime && smoothingSeconds <= 0));
            Start();
            Evaluate(5);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            playable.SetFloat(input, -0.125f);
            Evaluate(90);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f));
            if (!requiresMap) {
                playable.SetFloat(input, -0.2f);
                Evaluate(1);
                Assert.That(playable.GetFloat(output), Is.EqualTo(0.8f).Within(0.001f),
                    "Static unsmoothed actions must consume the current depth without a mapping AAP");
            }
            playable.SetFloat(input, -0.001f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.004f).Within(0.0001f), "Depth animations must follow values below 1% of the animation range");
            playable.SetFloat(input, 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.0001f));
        }

        [Test]
        public void InactiveDepthActionsInheritSharedAapFromLowerLayers(
            [Values(false, true)] bool optimize,
            [Values(0f, 0.1f)] float smoothingSeconds,
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool plugs,
            [Values(false, true)] bool sameOwner) {
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var output = fx.GetOrCreateFloat("Shared depth output");
            var baseline = VFClip.Create("Baseline");
            baseline.SetAap(output, 0.2f);
            fx.NewLayer("Baseline").NewState("Baseline").WithAnimation(baseline);
            var inputs = new[] { fx.CreateUniqueFloat("Lower depth", 0.1f), fx.CreateUniqueFloat("Upper depth", 0.1f) };
            for (var i = 0; i < inputs.Length; i++) {
                var owner = sameOwner ? root.asVf() : GameObjects.Create((plugs ? "Plug " : "Socket ") + i, root);
                if (!sameOwner || i == 0) {
                    if (plugs) owner.AddComponent<VRCFuryHapticPlug>();
                    else owner.AddComponent<VRCFuryHapticSocket>();
                }
                var action = new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                    smoothingSeconds = smoothingSeconds
                };
                var clip = VFClip.Create("Depth action " + i);
                clip.SetAap(output, motionTime ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
                var input = inputs[i];
                injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, owner,
                    owner.name, _ => _ => input, owner);
            }
            Start();
            void Check(float lower, float upper, float expected) {
                playable.SetFloat(inputs[0], lower);
                playable.SetFloat(inputs[1], upper);
                Evaluate(180);
                Assert.That(playable.GetFloat(output), Is.EqualTo(expected).Within(0.002f),
                    $"Lower depth {lower}, upper depth {upper}: inactive actions must inherit lower layers");
            }
            Check(0.1f, 0.1f, 0.2f);
            Check(-0.15f, 0.1f, 0.6f);
            Check(-0.15f, -0.2f, 0.8f);
            Check(-0.15f, 0.1f, 0.6f);
            Check(0.1f, 0.1f, 0.2f);
        }

        [Test]
        public void IndependentStaticDepthActionCanBeOptimized() {
            optimizeTrees = true;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Depth", 0.1f);
            var output = fx.GetOrCreateFloat("Independent output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), smoothingSeconds = 0
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Independent",
                _ => _ => input, root);
            Start();
            Assert.That(fx.GetLayers().Any(layer => layer.name.Contains("Independent - Depth Animations")), Is.False,
                "Independent actions should remain eligible for the layer optimizer");
            playable.SetFloat(input, -0.125f);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.001f));
            playable.SetFloat(input, 0.1f);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void LayerOptimizationRequiresAValidSampledBinding(
            [Values(false, true)] bool validOff,
            [Values(false, true)] bool validOn,
            [Values(false, true)] bool compound) {
            var input = fx.CreateUniqueFloat("Input");
            var flag = fx.CreateUniqueFloat("Flag");
            var output = fx.GetOrCreateFloat("Valid output");
            VFClip Clip(bool valid) {
                var clip = VFClip.Create("Binding validation");
                if (valid) clip.SetAap(output, 1);
                else clip.SetCurve(root, typeof(SkinnedMeshRenderer), "blendShape.Missing", 1);
                return clip;
            }
            var layer = fx.NewLayer("Binding validation");
            var off = layer.NewState("Off"); off.WithAnimation(Clip(validOff));
            var on = layer.NewState("On"); on.WithAnimation(Clip(validOn));
            // Valid bindings elsewhere in the layer do not make the sampled states useful.
            layer.NewState("Unreachable").WithAnimation(output.MakeSetter(1));
            var condition = input.IsGreaterThan(0);
            if (compound) condition = condition.And(flag.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            injector.GetService<LayerToTreeService>().Apply();
            Assert.That(layer.Exists(), Is.EqualTo(!validOff && !validOn));
        }

        [Test]
        public void CompoundConditionsCanOptimizeBuiltInIntsAboveOne([Values(false, true)] bool optimize) {
            optimizeTrees = optimize;
            var gesture = fx.GetOrCreateInt("GestureLeft");
            var enabled = fx.CreateUniqueBool("Enabled", true);
            var output = fx.GetOrCreateFloat("Compound output");
            var layer = fx.NewLayer("Compound int conditions");
            var off = layer.NewState("Off");
            var on = layer.NewState("On");
            var clip = VFClip.Create("Compound on");
            clip.SetAap(output, 7);
            on.WithAnimation(clip);
            var condition = gesture.IsNotEqualTo(1).And(enabled.IsTrue());
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!optimize));
            foreach (var value in new[] { 0, 1, 2, 7 }) {
                if (gesture.type == AnimatorControllerParameterType.Int) playable.SetInteger(gesture, value);
                else playable.SetFloat(gesture, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo(value != 1 ? 7 : 0).Within(0.001f));
            }
            if (enabled.type == AnimatorControllerParameterType.Bool) playable.SetBool(enabled, false);
            else playable.SetFloat(enabled, 0);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void NumericBooleanConditionsAcceptNegativeValues(
            [Values(false, true)] bool optimize,
            [Values(false, true)] bool invert,
            [Values(false, true)] bool integer) {
            var input = integer ? fx.CreateUniqueInt("Numeric input") : fx.CreateUniqueFloat("Numeric input");
            var gate = fx.CreateUniqueFloat("Gate", 1);
            var output = fx.GetOrCreateFloat("Numeric boolean output");
            var layer = fx.NewLayer("Numeric boolean conditions");
            var off = layer.NewState("Off"); off.WithAnimation(output.MakeSetter(0));
            var on = layer.NewState("On"); on.WithAnimation(output.MakeSetter(1));
            var condition = (invert ? input.IsFalse() : input.IsTrue()).And(gate.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            // Match production: optimize before the final parameter-type repair.
            if (optimize) injector.GetService<LayerToTreeService>().Apply();
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!optimize));
            foreach (var value in integer ? new[] { -2f, -1, 0, 1, 2 } : new[] { -1f, -0.5f, 0, 0.5f, 1 }) {
                if (input.type == AnimatorControllerParameterType.Int) playable.SetInteger(input, (int)value);
                else playable.SetFloat(input, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo((value != 0) != invert ? 1 : 0).Within(0.001f));
            }
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void ConditionNegationIsLimitedBeforeCartesianExpansion(int conditionCount, bool shouldOptimize) {
            var inputs = Enumerable.Range(0, conditionCount).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var positive = VFCondition.Any(inputs.Select(input => input.IsGreaterThan(0)));
            var negative = VFCondition.All(inputs.Select(input => input.IsGreaterThan(0).Not()));
            if (!shouldOptimize) {
                // A later tree-size rejection must not mask an unbounded negation.
                UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex("Contains more than six conditions before negation"));
            }
            CheckConditionGateLimit(positive, inputs, shouldOptimize, false, negative);
        }

        [Test]
        public void CompoundConditionOptimizationHasSixGateLimit(
            [Values(5, 6, 7)] int comparisons,
            [Values(false, true)] bool disjunction,
            [Values(false, true)] bool largePayload) {
            var inputs = Enumerable.Range(0, comparisons).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var conditions = inputs.Select(input => input.IsGreaterThan(0)).ToArray();
            var condition = disjunction ? VFCondition.Any(conditions) : VFCondition.All(conditions);
            CheckConditionGateLimit(condition, inputs, comparisons <= 6, largePayload);
        }

        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void CompoundConditionLimitCountsSharedTreesOnce(int pairs, bool shouldOptimize) {
            var inputs = Enumerable.Range(0, pairs * 2).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var condition = VFCondition.All(Enumerable.Range(0, pairs).Select(i =>
                inputs[i * 2].IsGreaterThan(0).Or(inputs[i * 2 + 1].IsGreaterThan(0))));
            // Construct the opposite directly instead of expanding the already-expanded positive DNF.
            var opposite = VFCondition.Any(Enumerable.Range(0, pairs).Select(i =>
                inputs[i * 2].IsGreaterThan(0).Not().And(inputs[i * 2 + 1].IsGreaterThan(0).Not())));
            CheckConditionGateLimit(condition, inputs, shouldOptimize, false, opposite);
        }

        [TestCase(true, true, true, true)]
        [TestCase(true, false, true, false)]
        [TestCase(false, false, true, true)]
        [TestCase(false, false, false, false)]
        public void ExactOppositesAreCheckedBeforeSimpleConditionExceptions(
            bool builtIn, bool exact, bool sameParameter, bool shouldOptimize) {
            optimizeTrees = true;
            var input = builtIn ? fx.GetOrCreateInt("GestureLeft") : fx.CreateUniqueInt("Input");
            var other = sameParameter ? input : fx.CreateUniqueInt("Other input");
            var output = fx.GetOrCreateFloat("Simple output");
            var layer = fx.NewLayer("Simple conditions");
            var off = layer.NewState("Off");
            off.WithAnimation(output.MakeSetter(0));
            var on = layer.NewState("On");
            on.WithAnimation(output.MakeSetter(7));
            on.TransitionsTo(off).When(input.IsEqualTo(1));
            off.TransitionsTo(on).When(other.IsNotEqualTo(exact ? 1 : 2));
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!shouldOptimize));
            if (!shouldOptimize) return;
            foreach (var value in new[] { 0, 1, 2, 7 }) {
                playable.SetFloat(input, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo(value == 1 ? 0 : 7).Within(0.001f));
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void TreeGrowthLimitSubtractsEachInputPayloadOnce(int depth) {
            optimizeTrees = true;
            var gate = fx.CreateUniqueFloat("Gate", 1);
            var flag = fx.CreateUniqueFloat("Flag", 1);
            var mix = fx.CreateUniqueFloat("Mix", 0.5f);
            var output = fx.GetOrCreateFloat("Shared payload output");
            var clip = output.MakeSetter(1);
            VFMotion payload = clip;
            for (var i = 0; i < depth; i++) {
                payload = VFBlendTree1D.CreateWithData("Shared payload " + i, mix, (0, clip), (1, payload));
            }
            var layer = fx.NewLayer("Shared payload");
            var off = layer.NewState("Off"); off.WithAnimation(payload);
            var on = layer.NewState("On"); on.WithAnimation(payload);
            var condition = gate.IsGreaterThan(0).And(flag.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.False, "The original payload of each input state must be subtracted once");
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
        }

        private void CheckConditionGateLimit(VFCondition condition, VFAParam[] inputs, bool shouldOptimize, bool largePayload,
            VFCondition opposite = null) {
            optimizeTrees = true;
            foreach (var input in inputs) input.MakeExpressionFloat(synced: false);
            var output = fx.GetOrCreateFloat("Budget output");
            var baseline = VFClip.Create("Budget baseline");
            baseline.SetAap(output, 0);
            fx.NewLayer("Budget baseline").NewState("Baseline").WithAnimation(baseline);
            var clip = VFClip.Create("Budget on");
            clip.SetAap(output, 1);
            VFMotion payload = clip;
            if (largePayload) {
                var mix = fx.CreateUniqueFloat("Payload mix", 0.5f);
                for (var i = 0; i < 8; i++) {
                    payload = VFBlendTree1D.CreateWithData("Existing payload " + i, mix, (0, clip), (1, payload));
                }
            }
            var layer = fx.NewLayer("Budget action");
            var off = layer.NewState("Off");
            var on = layer.NewState("On");
            on.WithAnimation(payload);
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(opposite ?? condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!shouldOptimize), "The gate budget must exclude the existing animation payload");
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
            foreach (var input in inputs) playable.SetFloat(input, 0);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmoothedDepthFoldsMappingAndPreservesTheResponseOneFrameEarlier(bool reverseMotion) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = 0.1f, reverseClip = reverseMotion
            };
            var clip = VFClip.Create("Depth action");
            clip.SetAap(fx.GetOrCreateFloat("Depth action output"), reverseMotion ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test", _ => _ => input, root);
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Mapped")), Is.False);
            var actual = fx.parameters.Single(p => p.name.Contains("/Smoothed/Pass2"));
            Assert.That(actual.GetDefault(), Is.EqualTo(reverseMotion ? 1 : 0));

            // Reproduce the former mapped-AAP + two-pass smoother with the same live speed.
            var speed = fx.parameters.Single(p => p.name.Contains("smoothingSpeed/"));
            var mapped = math.Map("Reference mapped", input, 0, -0.25f, 0, 1);
            mapped.SetDefault(actual.GetDefault());
            VFAParam ReferencePass(string name, VFAParam target) {
                var output = fx.CreateUniqueFloat(name, actual.GetDefault());
                tree.Add(VFBlendTree1D.CreateWithData(name, speed,
                    (0, output.MakeCopier(output)), (1, output.MakeCopier(target))));
                return output;
            }
            var reference = ReferencePass("Reference pass 2", ReferencePass("Reference pass 1", mapped));
            Start();
            Evaluate(120);
            foreach (var value in new[] { -0.125f, -0.5f, 0.2f, -0.2f }) {
                playable.SetFloat(input, value);
                var previous = playable.GetFloat(actual);
                for (var frame = 0; frame < 60; frame++) {
                    Evaluate(1);
                    Assert.That(playable.GetFloat(reference), Is.EqualTo(previous).Within(0.0001f),
                        $"Input {value}, frame {frame}");
                    previous = playable.GetFloat(actual);
                }
                Assert.That(playable.GetFloat(actual), Is.EqualTo(Mathf.Clamp01(value / -0.25f)).Within(0.001f));
            }
        }

        [TestCase(0f, "range")]
        [TestCase(0f, "input")]
        [TestCase(0f, "initial")]
        [TestCase(0.1f, "range")]
        [TestCase(0.1f, "input")]
        [TestCase(0.1f, "initial")]
        [TestCase(0.1f, "smoothing")]
        public void DepthActionsShareControlsOnlyWhenTheirSettingsMatch(float seconds, string difference) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var selfInput = fx.CreateUniqueFloat("Merged depth", 0);
            var otherInput = fx.CreateUniqueFloat("Others depth", 0);
            var actions = Enumerable.Range(0, 3).Select(i => {
                var different = i == 2;
                var action = new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(different && difference == "range" ? -0.5f : -0.25f, 0),
                    units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                    enableSelf = !(different && difference == "input"),
                    smoothingSeconds = different && difference == "smoothing" ? seconds * 2 : seconds,
                    reverseClip = different && difference == "initial"
                };
                var clip = VFClip.Create("MotionTime " + i);
                clip.SetAap(fx.GetOrCreateFloat("Output " + i), AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
                return action;
            }).ToArray();
            injector.GetService<SpsDepthAnimationService>().CreateAnims(actions, root, "Test",
                party => _ => party == Party.Merged ? selfInput : otherInput, root);
            var controls = fx.parameters.Where(p => seconds > 0 ? p.name.Contains("/Smoothed/Pass2") : p.name.Contains("/Mapped")).ToArray();
            Assert.That(controls, Has.Length.EqualTo(2), "The first two actions share a control; the third must remain independent");
            Assert.That(controls.Select(p => p.GetDefault()), Is.EquivalentTo(difference == "initial" ? new[] { 0f, 1f } : new[] { 0f, 0f }));
            Start();
            playable.SetFloat(selfInput, -0.125f);
            playable.SetFloat(otherInput, -0.2f);
            Evaluate(180);
            Assert.That(playable.GetFloat("Output 0"), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(playable.GetFloat("Output 1"), Is.EqualTo(0.5f).Within(0.01f));
            var expected = difference == "range" ? 0.25f : difference == "input" ? 0.8f : 0.5f;
            Assert.That(playable.GetFloat("Output 2"), Is.EqualTo(expected).Within(0.01f));
        }

        [TestCase(0f, false)]
        [TestCase(0.2f, false)]
        [TestCase(0f, true)]
        [TestCase(0.2f, true)]
        public void DisabledDepthActionsResetTheirInputs(float smoothingSeconds, bool reverseMotion) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var enabled = fx.CreateUniqueFloat("Socket enabled", 1);
            var output = fx.GetOrCreateFloat("Depth action output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = smoothingSeconds, reverseClip = reverseMotion
            };
            if (reverseMotion) {
                var clip = VFClip.Create("Reverse action");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            var actionRoot = GameObjects.Create("Action root", root);
            var on = VFClip.Create("Socket on"); on.SetEnabled(actionRoot, true);
            var off = VFClip.Create("Socket off"); off.SetEnabled(actionRoot, false);
            fx.NewLayer("Socket toggle").NewState("Socket toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Socket toggle", enabled, (0, off), (1, on)));
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test",
                _ => _ => input, actionRoot);
            var internalValues = fx.parameters.Where(p => p.name.Contains("/Mapped") || p.name.Contains("/Smoothed")).ToArray();
            Start();
            playable.SetFloat(input, reverseMotion ? 0 : -0.25f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
            playable.SetFloat(enabled, 0);
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            foreach (var parameter in internalValues) {
                Assert.That(parameter.GetDefault(), Is.EqualTo(reverseMotion ? 1 : 0));
                Assert.That(playable.GetFloat(parameter), Is.EqualTo(parameter.GetDefault()));
            }
            Evaluate(20);
            playable.SetFloat(input, reverseMotion ? -0.25f : 0);
            playable.SetFloat(enabled, 1);
            if (!reverseMotion) {
                Evaluate(1);
                Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "A re-enabled socket must not reuse its old smoothed penetration");
            }
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
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
            var menuToggle = useMenu ? fx.parameters.Single(p => p.name.EndsWith("_Test")) : null;
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
            Evaluate(3);
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

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void PlugCalculationAndRadiusGatesFollowParentAndSpsToggles(bool optimize, bool spsToggle) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var parent = GameObjects.Create("Plug parent", root);
            var obj = GameObjects.Create("Plug", parent);
            var plug = obj.AddComponent<VRCFuryHapticPlug>();
            plug.name = "Test";
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var action = new VRCFuryHapticSocket.DepthActionNew {
                units = VRCFuryHapticSocket.DepthActionUnits.Meters, smoothingSeconds = 0.2f
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = "Action output", value = 1 });
            plug.depthActions2.Add(action);
            var output = fx.GetOrCreateFloat("Action output");
            var injected = new[] {
                (SpsDepthMeters, -0.3f, 100f), (SpsVelocityMeters, -0.6f, 0f),
                (SpsDepthPlugLengths, -0.75f, 100f), (SpsPlugLengthMeters, 0.4f, 0.4f),
                (SpsPlugRadiusMeters, 0.05f, 0.05f), (SpsPlugLengthPlugLengths, 1f, 1f)
            };
            foreach (var (source, _, _) in injected) {
                fx.GetOrCreateFloat("Injected" + source, 77);
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = obj, sourceParam = source, resolvedParam = "Injected" + source
                });
            }
            var parentEnabled = fx.CreateUniqueFloat("Parent enabled", 1);
            parentEnabled.MakeExpressionFloat(synced: false);
            var parentOn = VFClip.Create("Parent on");
            var parentOff = VFClip.Create("Parent off");
            if (spsToggle) {
                parentOn.SetCurve(plug, "spsAnimatedEnabled", 1);
                parentOff.SetCurve(plug, "spsAnimatedEnabled", 0);
            } else {
                parentOn.SetEnabled(parent, true);
                parentOff.SetEnabled(parent, false);
            }
            fx.NewLayer("Parent toggle").NewState("Parent toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Parent toggle", parentEnabled, (0, parentOff), (1, parentOn)));
            var baker = injector.GetService<BakeHapticPlugsService>();
            baker.ApplyEarlyBake();
            baker.Apply();
            if (spsToggle) {
                // Exercise activation rewriting without requiring shader patching in this test.
                var skin = obj.AddComponent<MeshRenderer>();
                var rewrites = (List<BakeHapticPlugsService.SpsRewriteToDo>)typeof(BakeHapticPlugsService)
                    .VFField("spsRewritesToDo").GetValue(baker);
                rewrites.Add(new BakeHapticPlugsService.SpsRewriteToDo {
                    plugObject = obj, skin = skin, bakeRoot = injector.GetService<PlugFrameService>().Get(plug),
                    configureMaterial = (_, material) => material, spsBlendshapes = Array.Empty<string>()
                });
                baker.ApplySpsRewrites();
                // The gate only needs the rewritten activation curves, not material bindings.
                parentOn.SetCurve(skin, "material." + SpsConfigurer.SpsEnabled, null);
                parentOff.SetCurve(skin, "material." + SpsConfigurer.SpsEnabled, null);
                Assert.That(VF.Feature.Base.FeatureOrder.IsObjectEnabled,
                    Is.GreaterThan(VF.Feature.Base.FeatureOrder.HapticsAnimationRewrites));
                Assert.That(VF.Feature.Base.FeatureOrder.IsObjectEnabled,
                    Is.GreaterThan(VF.Feature.Base.FeatureOrder.TpsScaleFix));
            }
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            var othersName = receiverAnimator.GetParameter(receivers, false).name;
            var mergedName = receiverAnimator.GetParameter(receivers, Party.Merged).name;
            Start();
            var others = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == othersName);
            var merged = root.GetComponentsInChildren<VRCContactReceiver>(true).First(r => r.parameter == mergedName);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            var smoothingValues = fx.parameters.Where(p => p.name.Contains("/Mapped") || p.name.Contains("/Smoothed")).ToArray();
            void Contact(float proximity, float separation) {
                Set(others, proximity, separation);
                Set(merged, proximity, separation);
                playable.SetFloat(othersName + "_SenderVelocityZ", -0.6f);
                playable.SetFloat(mergedName + "_SenderVelocityZ", -0.6f);
            }
            Contact(1, 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.002f));
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.25f).Within(0.002f));
            foreach (var (source, active, _) in injected) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(active).Within(0.002f));
            }
            playable.SetFloat(parentEnabled, 0);
            Evaluate(3);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Assert.That(playable.GetFloat(position), Is.EqualTo(1).Within(0.001f));
            foreach (var (source, _, disabled) in injected) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(disabled).Within(0.002f));
            }
            foreach (var value in smoothingValues) Assert.That(playable.GetFloat(value), Is.Zero.Within(0.001f));
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => !string.IsNullOrEmpty(r.parameter))) {
                Assert.That(playable.IsParameterControlledByCurve(receiver.parameter), Is.False);
            }
            Evaluate(20);
            Contact(0, -1);
            playable.SetFloat(parentEnabled, 1);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Assert.That(playable.GetFloat("Injected" + SpsDepthMeters), Is.EqualTo(100).Within(0.001f));
        }
    }
}
#endif
