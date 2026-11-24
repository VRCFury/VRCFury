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
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.Dynamics;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class SpsPlugRadiusTestBase {
        protected const float MinimumRadius = 0.001f;
        protected GameObject root;
        protected VRCFuryHapticPlug plug;
        protected VRCFuryHapticPlugEditor.BakeResult bake;
        protected VFAvatarController fx;
        protected SpsPlugRadiusService service;
        protected VRCFuryInjector injector;

        protected string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        protected SelfAndOthersReceiverAnimatorService receiverAnimator;
        protected PlayableGraph graph;
        protected AnimatorControllerPlayable playable;
        protected string mergedParameterName;
        protected readonly List<Object> assets = new List<Object>();

        [SetUp]
        public void SetUp() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("SPS plug sender animation requires a desktop build target.");
            root = new GameObject("Radius sampling");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            root.transform.SetPositionAndRotation(new Vector3(3, 5, -7), Quaternion.Euler(17, 39, 23));
            root.transform.localScale = Vector3.one * 2;
            plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.useBoneMask = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.06f;
            plug.channelSettings.useSharedTag = false;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "first", allowSelf = true, allowOthers = false });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "second", allowSelf = false, allowOthers = true });
            var mesh = new Mesh {
                vertices = new[] { new Vector3(0.03f, 0, 0), new Vector3(0, 0.03f, 0),
                    new Vector3(-0.03f, 0, 0), new Vector3(0, -0.03f, 0), new Vector3(0, 0, 0.2f) },
                triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 }
            };
            assets.Add(mesh);
            var renderObject = GameObjects.Create("Mesh", root);
            renderObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            plug.configureTpsMesh.Add(renderObject.AddComponent<MeshRenderer>());
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            var controllers = injector.GetService<ControllersService>();
            controllers.ClearCache(applyBaseMask: false);
            fx = controllers.GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            service = injector.GetService<SpsPlugRadiusService>();
            receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            Assert.That(receivers.IsCreated, Is.False);
            bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            Assert.That(bake.socketReceivers, Is.SameAs(receivers));
            var scaleDetector = injector.GetService<WorldScaleDetectorService>();
            var inverseScale = fx.GetOrCreateFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            ((Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                    typeof(WorldScaleDetectorService).VFField("cache").GetValue(scaleDetector))
                [bake.bakeRoot] = (null, 0, new Lazy<VFAParam>(() => inverseScale));
        }

        [TearDown]
        public void TearDown() {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(root);
            foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        protected void Start(Action beforeFinalize = null) {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            Assert.Ignore("SPS radius sampling requires advanced contact parameters from VRCSDK 3.10.6 or newer.");
#endif
            service.Create(bake, "Test");
            mergedParameterName = receiverAnimator.GetParameter(bake.socketReceivers, Party.Merged).name;
            beforeFinalize?.Invoke();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller); assets.AddRange(context.NewAssets);
            var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Radius sampling"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimatorControllerPlayable.Create(graph, controller);
            AnimationPlayableOutput.Create(graph, "Radius", animator).SetSourcePlayable(playable);
            graph.Play();
        }

        protected VRCContactReceiver SourceReceiver(bool self) {
            return root.GetComponentsInChildren<VRCContactReceiver>(true)
                .Single(r => r.allowSelf == self && !r.name.Contains("Merged") && !string.IsNullOrEmpty(r.parameter));
        }

        protected VRCContactReceiver MergedReceiver(bool self) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.parameter == mergedParameterName && r.allowSelf == self && r.allowOthers != self);

        protected void SetMergedProximity(float proximity) {
            playable.SetFloat(mergedParameterName, proximity);
            playable.SetFloat(mergedParameterName + "_PointSeparation", proximity <= 0 ? -1
                : (1 - proximity) * bake.worldLength * SpsPlugReceiverService.RangeInPlugLengths);
        }

        protected class LegSpacingBones : SpsAutoChannelGenerator {
            public HumanBodyBones closest;
            public VFGameObject left;
            public VFGameObject right;
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => closest;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => bone switch {
                HumanBodyBones.LeftUpperLeg => left,
                HumanBodyBones.RightUpperLeg => right,
                _ => null
            };
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        // This integration probe calls the SDK 3.10.6 collision-job internals directly.
        protected sealed class ParameterAccess : IAnimParameterAccess {
            private readonly AnimatorControllerPlayable playable;
            private readonly string name;
            public ParameterAccess(AnimatorControllerPlayable playable, string name) { this.playable = playable; this.name = name; }
            public float floatVal { get => playable.GetFloat(name); set => playable.SetFloat(name, value); }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
        }

#endif
    }
}
