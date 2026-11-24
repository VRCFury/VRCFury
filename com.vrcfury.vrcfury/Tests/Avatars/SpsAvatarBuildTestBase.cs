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
    internal abstract class SpsAvatarBuildTestBase {
        protected GameObject root;
        protected VRCFuryInjector injector;

        protected string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        protected SelfAndOthersReceiverAnimatorService receiverAnimator;
        protected VFAvatarController fx;

        protected VFAParam SocketMenuParameter(string menuName) {
            var menu = injector.GetService<MenuService>().GetMenu().GetSubmenu("SPS");
            var name = menu.controls.Single(c => c.name == menuName).parameter.name;
            return fx.parameters.Single(p => p.name == name);
        }
        protected VFBlendTreeDirect tree;
        protected BlendtreeMath math;
        protected VFAParam scale;
        protected Lazy<VFAParam> inverseScale;
        protected Func<string, VFAParam> measurements;
        protected bool optimizeTrees;
        protected PlayableGraph graph;
        protected AnimatorControllerPlayable playable;
        protected readonly List<Object> assets = new();

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

        protected Func<Party, Func<string, VFAParam>> Socket(bool legacy = true) {
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.channelSettings.useSharedTag = legacy;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var service = injector.GetService<SpsSocketMeasurementsService>();
            var contacts = service.GetReceivers(socket, root, "Test");
            var detector = injector.GetService<WorldScaleDetectorService>();
            ((Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector))
                [root] = (scale, 1, inverseScale);
            return service.GetMeasurements(new VRCFuryHapticSocketEditor.BakeResult {
                bakeRoot = root,
                plugReceivers = contacts
            }, "Test");
        }

        protected Func<Party, Func<string, VFAParam>> PlugMeasurements(VRCFuryHapticPlug plug) {
            var bakeRoot = injector.GetService<PlugFrameService>().Get(plug);
            var bake = new VRCFuryHapticPlugEditor.BakeResult {
                channelContacts = injector.GetService<SpsChannelsService>().Get(plug),
                bakeRoot = bakeRoot,
                socketReceivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug),
                localLength = 0.4f, localRadius = 0.05f
            };
            var detector = injector.GetService<WorldScaleDetectorService>();
            ((Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector))
                [bakeRoot] = (scale, 1, inverseScale);
            return injector.GetService<SpsPlugMeasurementsService>().GetMeasurements(bake, "Plug");
        }

        protected VRCContactReceiver Main(bool self = true) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.allowSelf == self && !r.name.Contains("Merged") && r.collisionTags.Any(t => t.StartsWith(SpsTags.FormatPlug(""))));

        protected VRCContactReceiver Width(bool self = true) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.allowSelf == self && r.collisionTags.Contains(SpsTags.LegacyPlugWidth));

        protected VRCContactReceiver Merged() => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .First(r => r.name.Contains("Merged"));

        protected void Start() {
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

        protected void Set(VRCContactReceiver receiver, float proximity, float distance, float length = 1,
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

        protected void Evaluate(int frames = 12) {
            for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
        }

        protected sealed class TestParameterAccess : IAnimParameterAccess {
            private readonly Func<float> read;
            private readonly Action<float> write;
            public TestParameterAccess(Func<float> read, Action<float> write) { this.read = read; this.write = write; }
            public float floatVal { get => read(); set => write(value); }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
        }
    }
}
#endif
