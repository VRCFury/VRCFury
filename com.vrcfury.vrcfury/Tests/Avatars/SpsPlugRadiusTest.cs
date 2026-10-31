using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
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
    [Category("VRCFury")]
    public class SpsPlugRadiusTest {
        private GameObject root;
        private VRCFuryHapticPlug plug;
        private VRCFuryHapticPlugEditor.BakeResult bake;
        private ControllerManager fx;
        private SpsPlugRadiusService service;
        private VRCFuryInjector injector;
        private PlayableGraph graph;
        private AnimatorControllerPlayable playable;
        private string mergedParameterName;
        private readonly List<Object> assets = new List<Object>();

        private static void Inject(object target, string field, object value) => target.GetType().VFField(field).SetValue(target, value);

        [SetUp]
        public void SetUp() {
            root = new GameObject("Radius sampling");
            root.transform.SetPositionAndRotation(new Vector3(3, 5, -7), Quaternion.Euler(17, 39, 23));
            root.transform.localScale = Vector3.one * 2;
            plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.useBoneMask = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.06f;
            plug.useSharedTag = false;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "first", allowSelf = true, allowOthers = false });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "second", allowSelf = false, allowOthers = true });
            var mesh = new Mesh {
                vertices = new[] { new Vector3(0.03f, 0, 0), new Vector3(0, 0.03f, 0),
                    new Vector3(-0.03f, 0, 0), new Vector3(0, -0.03f, 0), new Vector3(0, 0, 0.2f) },
                triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 }
            };
            assets.Add(mesh);
            var renderObject = GameObjects.Create("Mesh", root);
            renderObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            plug.configureTpsMesh.Add(renderObject.AddComponent<MeshRenderer>());
            injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));

            var globals = new GlobalsService { avatarObject = root, allBuildersInRun = new List<FeatureBuilder>() };
            var sources = new LayerSourceService(); Inject(sources, "globals", globals);
            fx = new ControllerManager(VFController.Create(), null, VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0, () => "", name => name, sources);
            fx._NewFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            var controllers = new ControllersService();
            ((Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>)typeof(ControllersService).VFField("_controllers").GetValue(controllers))
                [VRCAvatarDescriptor.AnimLayerType.FX] = fx;
            var trees = new DbtLayerService(); Inject(trees, "controllers", controllers);
            service = new SpsPlugRadiusService();
            injector.Set(controllers);
            injector.ImportOne(typeof(SelfAndOthersReceiverAnimatorService));
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            Assert.That(receivers.IsCreated, Is.False);
            bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            Assert.That(bake.socketReceivers, Is.SameAs(receivers));
            Inject(service, "overlapping", new OverlappingContactsFixService());
            Inject(service, "trees", trees);
        }

        [TearDown]
        public void TearDown() {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(root);
            foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private void Start() {
            service.Create(plug, bake, "Test");
            mergedParameterName = bake.socketReceivers.GetMergedParameter().Name();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller); assets.AddRange(context.NewAssets);
            var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Radius sampling"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimatorControllerPlayable.Create(graph, controller);
            AnimationPlayableOutput.Create(graph, "Radius", animator).SetSourcePlayable(playable);
            graph.Play();
        }

        private VRCContactReceiver SourceReceiver(bool self) {
            var parameter = bake.socketReceivers.GetParameter(self).Name();
            return root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == parameter);
        }

        private VRCContactReceiver MergedReceiver(bool self) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.parameter == mergedParameterName && r.allowSelf == self && r.allowOthers != self);

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ParameterRequestsReuseShaderReceiversAndUnionAdvancedOutputs(bool self, bool shaderFirst) {
            var receivers = bake.socketReceivers;
            var targets = Enumerable.Range(0, 9)
                .Select(i => (Renderer)GameObjects.Create("Target " + i, root).AddComponent<MeshRenderer>()).ToArray();
            void Materials() => receivers.SetMaterials(targets, self, true, "ShaderOutput");
            if (shaderFirst) Materials();
            var parameter = receivers.GetParameter(self, ContactReceiver.AdvancedParameter.SenderLocalPosition);
            if (!shaderFirst) Materials();
            var again = receivers.GetParameter(self, ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.Name(), Is.EqualTo(parameter.Name()));
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(all, Has.Length.EqualTo(3));
            Assert.That(all.All(r => r.allowSelf == self && r.allowOthers != self && r.writeToMaterials), Is.True);
            Assert.That(all.Count(r => !string.IsNullOrEmpty(r.parameter)), Is.EqualTo(1));
            var receiver = all.Single(r => r.parameter == parameter.Name());
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
            foreach (var suffix in new[] { "_SenderLocalPositionX", "_SenderLocalPositionY", "_SenderLocalPositionZ",
                         "_SenderLossyScaleX", "_SenderLossyScaleY", "_SenderLossyScaleZ" }) {
                Assert.That(fx.GetParam(parameter.Name() + suffix), Is.Not.Null);
            }
            var parameterCount = fx.parameters.Length;
            receivers.GetParameter(self);
            Assert.That(fx.parameters, Has.Length.EqualTo(parameterCount));
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
        }

        [Test]
        public void MergedParameterRequestsUnionOutputsWithoutChangingSourceFlagsOrAddingSelectors() {
            var receivers = bake.socketReceivers;
            var parameter = receivers.GetMergedParameter(ContactReceiver.AdvancedParameter.SenderType);
            mergedParameterName = parameter.Name();
            var layers = fx.GetLayers().Count();
            var again = receivers.GetMergedParameter(ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.Name(), Is.EqualTo(parameter.Name()));
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
            foreach (var self in new[] { true, false }) {
                Assert.That(MergedReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderType
                    | ContactReceiver.AdvancedParameter.SenderLossyScale));
                Assert.That(SourceReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.PointSeparation));
            }
            Assert.That(fx.GetParam(parameter.Name() + "_SenderType"), Is.Not.Null);
            Assert.That(fx.GetParam(parameter.Name() + "_SenderLossyScaleY"), Is.Not.Null);
        }
#endif

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void SamplesCurveAtNormalizedSocketDistanceAndTracksAvatarScale(float distance) {
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            // Actual receiver proximity for a point sender distance*plugLength from the plug base.
            playable.SetFloat(self.parameter, Mathf.Clamp01(1 - distance / SpsPlugRadiusService.RangeInPlugLengths));
            playable.SetFloat(other.parameter, 0);
            playable.SetFloat(MergedReceiver(true).parameter,
                Mathf.Clamp01(1 - distance / SpsPlugRadiusService.RangeInPlugLengths));
            foreach (var scale in new[] { 1f, 3f, 0.5f }) {
                root.transform.localScale = Vector3.one * (2 * scale);
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                var actual = bake.mainSender.worldScale;
                var expected = 0.06f * Mathf.Clamp01(1 - distance) * scale;
                Assert.That(actual.y, Is.EqualTo(expected).Within(0.00002f));
                Assert.That(actual.x, Is.EqualTo(0.06f * scale).Within(0.00002f));
                Assert.That(actual.z, Is.EqualTo(0.4f * scale).Within(0.00002f));
                var sender = bake.mainSender.GetComponent<VRCContactSender>();
                Assert.That(sender.radius * Mathf.Max(actual.x, actual.y, actual.z), Is.EqualTo(0.4f * scale).Within(0.00002f));
                Assert.That(self.radius * self.transform.lossyScale.x, Is.EqualTo(0.64f * scale).Within(0.00002f));
            }
        }

        [Test]
        public void SamplesNearestPartyAndClampsToTipWithoutContacts() {
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            Assert.That(self.collisionTags, Is.EqualTo(new[] { "SPS/Socket/first" }));
            Assert.That(other.collisionTags, Is.EqualTo(new[] { "SPS/Socket/second" }));
            foreach (var values in new[] { (0.75f, 0.25f, 0.045f), (0.25f, 0.75f, 0.045f), (2f, 2f, 0f) }) {
                playable.SetFloat(self.parameter, Mathf.Clamp01(1 - values.Item1 / SpsPlugRadiusService.RangeInPlugLengths));
                playable.SetFloat(other.parameter, Mathf.Clamp01(1 - values.Item2 / SpsPlugRadiusService.RangeInPlugLengths));
                playable.SetFloat(MergedReceiver(true).parameter,
                    Mathf.Clamp01(1 - Mathf.Min(values.Item1, values.Item2) / SpsPlugRadiusService.RangeInPlugLengths));
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(values.Item3).Within(0.00002f));
            }
        }

        [Test]
        public void ReciprocalDoesNotBlendInItsNonzeroParameterDefault() {
            var tree = VFBlendTreeDirect.Create("Reciprocal regression");
            fx.NewLayer("Reciprocal regression").NewState("DBT").WithAnimation(tree);
            var input = fx.NewFloat("Reciprocal input", def: 2);
            var reciprocal = new BlendtreeMath(fx, tree).Invert("Reciprocal output", input);
            Start();
            foreach (var value in new[] { 2f, 4f, 0.5f }) {
                playable.SetFloat(input.Name(), value);
                for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                Assert.That(playable.GetFloat(reciprocal.Name()), Is.EqualTo(1 / value).Within(0.0001f));
            }
        }

        [Test]
        public void ManualPlugWithoutMeshUsesConstantRadius() {
            plug.configureTpsMesh.Clear();
            var injector = new VRCFuryInjector(); injector.ImportScan(typeof(VFServiceAttribute));
            bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            foreach (var depth in new[] { 0f, 0.1f, 0.3f, 0.4f })
                Assert.That(bake.radiusCurve.Evaluate(depth), Is.EqualTo(0.06f).Within(0.00001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShaderAndAnimatorReuseOneLazyReceiverRequest(bool shaderFirst) {
            Assert.That(bake.socketReceivers.IsCreated, Is.False);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(), Is.Empty);
            var receiverService = injector.GetService<SpsPlugReceiverService>();
            Assert.That(receiverService.GetReceivers(plug), Is.SameAs(bake.socketReceivers));
            void Shader() => receiverService.Create(plug,
                new[] { plug.configureTpsMesh.Single() });
            if (shaderFirst) {
                Shader();
                Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(3));
                Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true).All(r => r.writeToMaterials), Is.True);
            }
            Start();
            if (!shaderFirst) Shader();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(5)); // Quartet plus shader-only path buffer.
            var r = bake.socketReceivers;
            Assert.That(SourceReceiver(true).writeToMaterials && SourceReceiver(false).writeToMaterials, Is.True);
            Assert.That(MergedReceiver(true).writeToMaterials || MergedReceiver(false).writeToMaterials, Is.False);
            Assert.That(SourceReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.EqualTo(MergedReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(true).parameter).And.Not.EqualTo(SourceReceiver(false).parameter));
            var again = r.GetMergedParameter();
            Assert.That(again.Name(), Is.EqualTo(MergedReceiver(true).parameter));
        }

        [Test]
        public void SelectsOneMergedReceiverWithoutBufferingItsParameter() {
            Start();
            var r = bake.socketReceivers;
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
            Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.False);
            Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.True);
            foreach (var values in new[] { (0.9f, 0.1f, true), (0.1f, 0.9f, false), (0.8f, 0.2f, true), (0f, 0f, false) }) {
                playable.SetFloat(SourceReceiver(true).parameter, values.Item1);
                playable.SetFloat(SourceReceiver(false).parameter, values.Item2);
                // SDK output deliberately differs from both selector inputs. Sampling must use
                // this current merged value immediately, rather than max/self/other from a DBT.
                playable.SetFloat(MergedReceiver(true).parameter, 0.6f);
                graph.Evaluate(1f / 60);
                Assert.That(SourceReceiver(true).gameObject.activeSelf && SourceReceiver(false).gameObject.activeSelf, Is.True);
                Assert.That(MergedReceiver(true).enabled && MergedReceiver(false).enabled, Is.True);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item3));
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(!values.Item3));
                Assert.That(playable.IsParameterControlledByCurve(MergedReceiver(true).parameter), Is.False);
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(0.0216f).Within(0.00002f));
            }
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [Test]
        public void SaturatedProximitiesSelectNearestPointSeparationWithoutBuffering() {
            Start();
            var self = SourceReceiver(true).parameter;
            var others = SourceReceiver(false).parameter;
            foreach (var values in new[] {
                         (1f, 1f, 0.1f, 0.3f, true),
                         (1f, 1f, 0.3f, 0.1f, false),
                         (1f, 1f, 0.2f, 0.2f, false),
                         (1f, 1f, 0f, 0.1f, true),
                         (1f, 0.9f, 0.3f, 0.1f, true),
                         (0.9f, 1f, 0.1f, 0.3f, false),
                         (0f, 0f, -1f, -1f, false),
                         (1f, 1f, 0.1f, 0.3f, true),
                         (VRCFuryEditorUtils.NextFloatDown(1), 1f, 0.1f, 0.3f, false),
                         (1f, VRCFuryEditorUtils.NextFloatDown(1), 0.3f, 0.1f, true)
                     }) {
                playable.SetFloat(self, values.Item1);
                playable.SetFloat(others, values.Item2);
                playable.SetFloat(self + "_PointSeparation", values.Item3);
                playable.SetFloat(others + "_PointSeparation", values.Item4);
                playable.SetFloat(mergedParameterName, 0.6f);
                graph.Evaluate(1f / 60);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item5), values.ToString());
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(!values.Item5), values.ToString());
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(0.0216f).Within(0.00002f));
            }
        }
#endif

        [Test]
        public void ExtraMaterialReceiversHaveNoParametersOrMergedCopies() {
            Start();
            var renderers = new List<Renderer> { plug.configureTpsMesh.Single() };
            for (var i = 1; i < 9; i++) renderers.Add(GameObjects.Create("Extra renderer " + i, root).AddComponent<MeshRenderer>());
            var receiverService = injector.GetService<SpsPlugReceiverService>();
            receiverService.Create(plug, renderers);
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(receivers, Has.Length.EqualTo(11)); // Six entrance writers, two merged, three path writers.
            Assert.That(receivers.Count(r => !r.writeToMaterials), Is.EqualTo(2));
            Assert.That(receivers.Where(r => r.writeToMaterials && r.separateShaderParameter != "SPS_Path")
                .Count(r => !string.IsNullOrEmpty(r.parameter)), Is.EqualTo(2));
            foreach (var name in new[] { "SPS_Self", "SPS_Others", "SPS_Path" }) {
                Assert.That(receivers.Where(r => r.writeToMaterials && r.separateShaderParameter == name)
                    .SelectMany(r => r.shaderTargetRenderers), Is.EquivalentTo(renderers));
            }
        }

        private sealed class ParameterAccess : IAnimParameterAccess {
            private readonly AnimatorControllerPlayable playable;
            private readonly string name;
            public ParameterAccess(AnimatorControllerPlayable playable, string name) { this.playable = playable; this.name = name; }
            public float floatVal { get => playable.GetFloat(name); set => playable.SetFloat(name, value); }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
        }

        [Test]
        public void SdkMergedReceiverWritesCurrentValueAfterPartySwitch() {
            Start();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo Field(Type type, string name) {
                for (; type != null; type = type.BaseType) {
                    var field = type.GetField(name, flags);
                    if (field != null) return field;
                }
                throw new Exception("Missing field " + name);
            }
            object Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
            void Set(object obj, string name, object value) => Field(obj.GetType(), name).SetValue(obj, value);
            object Call(object obj, string name, params object[] args) {
                for (var type = obj.GetType(); type != null; type = type.BaseType) {
                    var method = type.GetMethods(flags | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
                    if (method != null) return method.Invoke(obj, args);
                }
                throw new Exception("Missing method " + name);
            }
            var managerObj = new GameObject("Radius SDK collision test");
            var previousManager = ContactManager.Inst;
            ContactManager manager = null;
            try {
                manager = managerObj.AddComponent<ContactManager>();
                Field(typeof(ContactManager), "Inst").SetValue(null, manager);
                if (!(bool)Get(manager, "hasInit")) Call(manager, "Init");
                var receivers = bake.socketReceivers;
                foreach (var receiver in new[] { SourceReceiver(true), SourceReceiver(false), MergedReceiver(true), MergedReceiver(false) }) {
                    receiver.playerId = 1;
                    Set(receiver, "_assignedUsage", DynamicsUsage.Avatar);
                    receiver.allowInit = true;
                    if (receiver.gameObject.activeInHierarchy) Call(receiver, "Start");
                    receiver.paramAccess = new ParameterAccess(playable, receiver.parameter);
                }
                VRCContactSender Sender(string tag, int player) {
                    var obj = GameObjects.Create("SDK sender " + player, root);
                    var sender = obj.AddComponent<VRCContactSender>();
                    sender.radius = 0; sender.playerId = player;
                    Set(sender, "_assignedUsage", DynamicsUsage.Avatar);
                    sender.collisionTags = new List<string> { tag };
                    sender.allowInit = true;
                    Call(sender, "Start");
                    return sender;
                }
                var self = Sender("SPS/Socket/first", 1);
                var other = Sender("SPS/Socket/second", 2);
                void Tick() {
                    var method = typeof(ContactManager).GetMethod("ScheduleUpdateReceiversJob", flags);
                    var job = method.Invoke(manager, new[] { Activator.CreateInstance(method.GetParameters()[0].ParameterType) });
                    job.GetType().GetMethod("Complete").Invoke(job, null);
                    Call(Get(manager, "collision"), "Complete");
                    Call(manager, "HandleDynamicsFrameComplete");
                }
                // Repeatedly switch while both senders stay overlapping. Let the SDK deliver
                // collision-enter events as well as its receiver job; no Animator/DBT evaluation
                // occurs between those steps or between the SDK write and the value assertion.
                foreach (var distances in new[] { (0.2f, 0.7f), (0.8f, 0.3f), (0.4f, 0.9f), (0.9f, 0.1f) }) {
                    self.transform.position = bake.bakeRoot.TransformPoint(Vector3.forward * (0.2f * distances.Item1));
                    other.transform.position = bake.bakeRoot.TransformPoint(Vector3.forward * (0.2f * distances.Item2));
                    Tick();
                    Tick();
                    Assert.That(playable.GetFloat(SourceReceiver(true).parameter),
                        Is.EqualTo(1 - distances.Item1 / SpsPlugRadiusService.RangeInPlugLengths).Within(0.0001f),
                        "self source; usage=" + SourceReceiver(true).Usage + "/" + self.Usage
                        + " player=" + Call(SourceReceiver(true), "GetPlayerId") + "/" + Call(self, "GetPlayerId")
                        + " raw=" + SourceReceiver(true).paramValue
                        + " records=" + ((System.Collections.ICollection)Get(SourceReceiver(true), "collisionRecords")).Count);
                    Assert.That(playable.GetFloat(SourceReceiver(false).parameter),
                        Is.EqualTo(1 - distances.Item2 / SpsPlugRadiusService.RangeInPlugLengths).Within(0.0001f), "others source");
                    graph.Evaluate(1f / 60);
                    foreach (var receiver in new[] { MergedReceiver(true), MergedReceiver(false) }) {
                        // Synchronous EditMode evaluation does not run the normal Start loop.
                        // Initialize a newly activated receiver; dispatch lifecycle calls only
                        // when Unity has not already registered/removed it with the SDK manager.
                        if (receiver.gameObject.activeInHierarchy) {
                            if (!(bool)Get(receiver, "hasInit")) Call(receiver, "Start");
                            else if (Get(receiver, "manager") == null) Call(receiver, "OnEnable");
                        } else if (Get(receiver, "manager") != null) Call(receiver, "OnDisable");
                    }
                    Tick();
                    Tick();
                    var expected = 1 - Mathf.Min(distances.Item1, distances.Item2) / SpsPlugRadiusService.RangeInPlugLengths;
                    Assert.That(playable.GetFloat(MergedReceiver(true).parameter), Is.EqualTo(expected).Within(0.0001f),
                        "self active=" + MergedReceiver(true).gameObject.activeSelf + " others active=" + MergedReceiver(false).gameObject.activeSelf);
                    graph.Evaluate(1f / 60);
                    Assert.That(bake.mainSender.worldScale.y,
                        Is.EqualTo(0.06f * (1 - Mathf.Min(distances.Item1, distances.Item2))).Within(0.0001f));
                }
            } finally {
                // Contact destruction must happen while its manager and parameter accesses are valid.
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObj);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
        }
    }
}
