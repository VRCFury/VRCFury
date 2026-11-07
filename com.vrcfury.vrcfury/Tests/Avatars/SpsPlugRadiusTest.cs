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
    [Category("VRCFury")]
    public class SpsPlugRadiusTest {
        private const float MinimumRadius = 0.001f;
        private GameObject root;
        private VRCFuryHapticPlug plug;
        private VRCFuryHapticPlugEditor.BakeResult bake;
        private VFAvatarController fx;
        private SpsPlugRadiusService service;
        private VRCFuryInjector injector;
        private SelfAndOthersReceiverAnimatorService receiverAnimator;
        private PlayableGraph graph;
        private AnimatorControllerPlayable playable;
        private string mergedParameterName;
        private readonly List<Object> assets = new List<Object>();

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
            ((Dictionary<VFGameObject, (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse)>)
                    typeof(WorldScaleDetectorService).VFField("cache").GetValue(scaleDetector))
                [bake.bakeRoot] = (null, null, new Lazy<VFAParam>(() => inverseScale));
        }

        [TearDown]
        public void TearDown() {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(root);
            foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private void Start() {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            Assert.Ignore("SPS radius sampling requires advanced contact parameters from VRCSDK 3.10.6 or newer.");
#endif
            service.Create(bake, "Test");
            mergedParameterName = receiverAnimator.GetParameter(bake.socketReceivers, Party.Merged).name;
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

        private VRCContactReceiver SourceReceiver(bool self) {
            var parameter = receiverAnimator.GetParameter(bake.socketReceivers, self).name;
            return root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == parameter);
        }

        private VRCContactReceiver MergedReceiver(bool self) => root.GetComponentsInChildren<VRCContactReceiver>(true)
            .Single(r => r.parameter == mergedParameterName && r.allowSelf == self && r.allowOthers != self);

        private void SetMergedProximity(float proximity) {
            playable.SetFloat(mergedParameterName, proximity);
            playable.SetFloat(mergedParameterName + "_PointSeparation", proximity <= 0 ? -1
                : (1 - proximity) * bake.worldLength * SpsPlugReceiverService.RangeInPlugLengths);
        }

        [TestCase(1f, false)]
        [TestCase(3f, false)]
        [TestCase(1f, true)]
        public void DisabledPlugResetsRadiusSamplingAndKeepsSenderInvertible(float resize, bool emptyProfile) {
            if (emptyProfile) bake.radiusCurve = AnimationCurve.Constant(0, 1, 0);
            var defaultScale = bake.mainSender.localScale;
            var enabled = fx.GetOrCreateFloat("TestPlugEnabled", 1);
            var on = VFClip.Create("Plug on"); on.SetEnabled(bake.bakeRoot, true);
            var off = VFClip.Create("Plug off"); off.SetEnabled(bake.bakeRoot, false);
            fx.NewLayer("Plug toggle").NewState("Plug toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Plug toggle", enabled, (0, off), (1, on)));
            Start();
            root.transform.localScale *= resize;
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            SetMergedProximity(1);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            playable.SetFloat(enabled, 0);
            for (var i = 0; i < 2; i++) graph.Evaluate(1f / 60);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            Assert.That(playable.GetFloat(position), Is.EqualTo(1));
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(bake.worldRadius).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.x, Is.GreaterThan(0));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.0001f));
            Assert.That(bake.mainSender.localToWorldMatrix.determinant, Is.GreaterThan(0));
            Assert.That(bake.mainSender.localScale, Is.EqualTo(defaultScale));
            Assert.That(playable.IsParameterControlledByCurve(mergedParameterName), Is.False);
            playable.SetFloat(enabled, 1);
            playable.SetFloat(mergedParameterName + "_PointSeparation", bake.worldLength / 2);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.5f).Within(0.0001f));
            var expected = emptyProfile ? MinimumRadius
                : Mathf.Lerp(0.06f, MinimumRadius, 0.5f);
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(expected * resize).Within(0.0001f));
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void NamedOgbParameterIsReusedByDepthAndShaderConsumers(bool self, bool shaderFirst) {
            var receivers = bake.socketReceivers;
            var parameterName = "OGB/Plug/Test/" + (self ? "DepthSelf" : "DepthOthers");
            var renderer = plug.configureTpsMesh.Single();
            void Materials() => receivers.SetMaterials(new[] { renderer }, self, false, "ShaderOutput");
            if (shaderFirst) Materials();
            var ogb = receiverAnimator.GetParameter(receivers, self, parameterName, ContactReceiver.AdvancedParameter.SenderType);
            if (!shaderFirst) Materials();
            var depth = receiverAnimator.GetParameter(receivers, self, advancedParameters: ContactReceiver.AdvancedParameter.PointSeparation);
            Assert.That(ogb.name, Is.EqualTo(parameterName));
            Assert.That(depth.name, Is.EqualTo(parameterName));
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameterName), Is.Not.Null);
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single();
            Assert.That(receiver.parameter, Is.EqualTo(parameterName));
            Assert.That(receiver.writeToMaterials, Is.True);
            Assert.That(receiver.shaderTargetRenderers, Is.EqualTo(new[] { renderer }));
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderType
                | ContactReceiver.AdvancedParameter.PointSeparation));
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameterName + "_PointSeparation"), Is.Not.Null);
            Assert.That(receiverAnimator.GetParameter(receivers, self, parameterName).name, Is.EqualTo(parameterName));
            receiverAnimator.GetParameter(receivers, Party.Merged);
            Assert.That(SourceReceiver(self), Is.SameAs(receiver));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
        }

        [Test]
        public void DifferentExplicitParameterNamesDoNotRenameExistingReceivers() {
            var receivers = bake.socketReceivers;
            var first = receiverAnimator.GetParameter(receivers, true, "OGB/First");
            var second = receiverAnimator.GetParameter(receivers, true, "OGB/Second");
            Assert.That(first.name, Is.EqualTo("OGB/First"));
            Assert.That(second.name, Is.EqualTo("OGB/Second"));
            Assert.That(receiverAnimator.GetParameter(receivers, true).name, Is.EqualTo(first.name));
            Assert.That(receiverAnimator.GetParameter(receivers, true, "OGB/Second").name, Is.EqualTo(second.name));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true).Select(r => r.parameter),
                Is.EquivalentTo(new[] { "OGB/First", "OGB/Second" }));
        }

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
            var parameter = receiverAnimator.GetParameter(receivers, self, advancedParameters: ContactReceiver.AdvancedParameter.SenderLocalPosition);
            if (!shaderFirst) Materials();
            var again = receiverAnimator.GetParameter(receivers, self, advancedParameters: ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.name, Is.EqualTo(parameter.name));
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(all, Has.Length.EqualTo(3));
            Assert.That(all.All(r => r.allowSelf == self && r.allowOthers != self && r.writeToMaterials), Is.True);
            Assert.That(all.Count(r => !string.IsNullOrEmpty(r.parameter)), Is.EqualTo(1));
            var receiver = all.Single(r => r.parameter == parameter.name);
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
            foreach (var suffix in new[] { "_SenderLocalPositionX", "_SenderLocalPositionY", "_SenderLocalPositionZ",
                         "_SenderLossyScaleX", "_SenderLossyScaleY", "_SenderLossyScaleZ" }) {
                Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameter.name + suffix), Is.Not.Null);
            }
            var parameterCount = fx.parameters.Length;
            receiverAnimator.GetParameter(receivers, self);
            Assert.That(fx.parameters, Has.Length.EqualTo(parameterCount));
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
        }

        [Test]
        public void MergedParameterRequestsUnionOutputsWithoutChangingSourceFlagsOrAddingSelectors() {
            var receivers = bake.socketReceivers;
            var parameter = receiverAnimator.GetParameter(receivers, Party.Merged, advancedParameters: ContactReceiver.AdvancedParameter.SenderType);
            mergedParameterName = parameter.name;
            var layers = fx.GetLayers().Count();
            var again = receiverAnimator.GetParameter(receivers, Party.Merged, advancedParameters: ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.name, Is.EqualTo(parameter.name));
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
            foreach (var self in new[] { true, false }) {
                Assert.That(MergedReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderType
                    | ContactReceiver.AdvancedParameter.SenderLossyScale));
                Assert.That(SourceReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.PointSeparation));
            }
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameter.name + "_SenderType"), Is.Not.Null);
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameter.name + "_SenderLossyScaleY"), Is.Not.Null);
        }
#endif

        [Test]
        public void RadiusOffsetMaterialUsesProfileWithoutChangingTheBakedRadius() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            Assert.Ignore("SPS deformation requires material contact support.");
#endif
            var renderer = bake.renderers.Single();
            renderer.spsBlendshapes = new List<string>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            SpsPatcher.Patch(material, false, false);
            material.SetFloat(SpsConfigurer.SpsBakedRadius, 0.06f);
            renderer.renderer.sharedMaterial = material;
            Assert.That(material.GetFloat(SpsConfigurer.SpsRadiusOffsetMultiplier), Is.EqualTo(1));
            for (var pass = 0; pass < material.passCount; pass++) UnityEditor.ShaderUtil.CompilePass(material, pass, true);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(material.shader), Is.False);
            Start();
            var block = new MaterialPropertyBlock();
            var parameterCount = fx.parameters.Length;
            foreach (var resize in new[] { 0.5f, 1f, 3f }) foreach (var distance in new[] { 0f, 0.5f, 1f, 1.5f }) {
                root.transform.localScale = Vector3.one * (2 * resize);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                SetMergedProximity(1);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                renderer.renderer.GetPropertyBlock(block);
                var multiplier = block.GetFloat(SpsConfigurer.SpsRadiusOffsetMultiplier);
                Assert.That(multiplier, Is.EqualTo(Mathf.Clamp01(1 - distance)).Within(0.0001f));
                Assert.That(material.GetFloat(SpsConfigurer.SpsBakedRadius), Is.EqualTo(0.06f));
                Assert.That(0.06f * resize * multiplier,
                    Is.EqualTo(Mathf.Max(0, bake.radiusCurve.Evaluate(distance)) * bake.bakeRoot.worldScale.x).Within(0.0001f));
                Assert.That(bake.mainSender.worldScale.y, Is.GreaterThan(0), "A zero material radius must not make the sender matrix singular");
            }
            Assert.That(fx.parameters.Length, Is.EqualTo(parameterCount));
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void SamplesCurveAtNormalizedSocketDistanceAndTracksAvatarScale(float distance) {
            Assert.That(bake.radiusCurve.keys.First().time, Is.Zero);
            Assert.That(bake.radiusCurve.keys.Last().time, Is.EqualTo(1));
            Assert.That(bake.radiusCurve.Evaluate(distance),
                Is.EqualTo(Mathf.Lerp(0.06f, 0, Mathf.Clamp01(distance))).Within(0.000001f));
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            // Actual receiver proximity for a point sender distance*plugLength from the plug base.
            playable.SetFloat(self.parameter, Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            playable.SetFloat(other.parameter, 0);
            SetMergedProximity(
                Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            foreach (var scale in new[] { 1f, 3f, 0.5f }) {
                root.transform.localScale = Vector3.one * (2 * scale);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                var actual = bake.mainSender.worldScale;
                var expected = Mathf.Lerp(0.06f, MinimumRadius, Mathf.Clamp01(distance)) * scale;
                Assert.That(actual.y, Is.EqualTo(expected).Within(0.00002f));
                Assert.That(actual.y, Is.GreaterThan(0));
                Assert.That(actual.x, Is.EqualTo(0.06f * scale).Within(0.00002f));
                Assert.That(actual.z, Is.EqualTo(0.4f * scale).Within(0.00002f));
                var sender = bake.mainSender.GetComponent<VRCContactSender>();
                Assert.That(sender.radius * Mathf.Max(actual.x, actual.y, actual.z), Is.EqualTo(0.4f * scale).Within(0.00002f));
                Assert.That(self.radius * self.transform.lossyScale.x, Is.EqualTo(0.64f * scale).Within(0.00002f));
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        public void SamplesPointSeparationIndependentlyOfProximityAndItsThreeMeterCap(float scale) {
            Start();
            root.transform.localScale = Vector3.one * (2 * scale);
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            var distance = 0.5f * bake.worldLength;
            foreach (var proximity in new[] { 1 - distance / Mathf.Min(3, 0.64f * scale), 1f, 0.1f }) {
                playable.SetFloat(mergedParameterName, proximity);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance);
                for (var i = 0; i < 8; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y,
                    Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, 0.5f) * scale).Within(0.0001f));
            }
            playable.SetFloat(mergedParameterName, 0);
            playable.SetFloat(mergedParameterName + "_PointSeparation", -1);
            for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(MinimumRadius * scale).Within(0.0001f));
            playable.SetFloat(mergedParameterName, 1);
            playable.SetFloat(mergedParameterName + "_PointSeparation", 0);
            for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(0.06f * scale).Within(0.0001f));
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public void PostBakeResizingPreservesProfilePositionAndLocalRadius(float scale) {
            root.transform.localScale *= scale;
            Start();
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            playable.SetFloat(mergedParameterName, 1 - 0.5f / SpsPlugReceiverService.RangeInPlugLengths);
            playable.SetFloat(mergedParameterName + "_PointSeparation", bake.worldLength / 2);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.y,
                Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, 0.5f) * scale).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.0001f));
        }

        [Test]
        public void SamplesNearestPartyAndClampsToTipWithoutContacts() {
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            Assert.That(self.collisionTags, Is.EqualTo(new[] { "SPS/Socket/first" }));
            Assert.That(other.collisionTags, Is.EqualTo(new[] { "SPS/Socket/second" }));
            var quarterRadius = Mathf.Lerp(0.06f, MinimumRadius, 0.25f);
            foreach (var values in new[] { (0.75f, 0.25f, quarterRadius), (0.25f, 0.75f, quarterRadius), (2f, 2f, 0f) }) {
                playable.SetFloat(self.parameter, Mathf.Clamp01(1 - values.Item1 / SpsPlugReceiverService.RangeInPlugLengths));
                playable.SetFloat(other.parameter, Mathf.Clamp01(1 - values.Item2 / SpsPlugReceiverService.RangeInPlugLengths));
                SetMergedProximity(
                    Mathf.Clamp01(1 - Mathf.Min(values.Item1, values.Item2) / SpsPlugReceiverService.RangeInPlugLengths));
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y,
                    Is.EqualTo(Mathf.Max(MinimumRadius, values.Item3)).Within(0.00002f));
                Assert.That(bake.mainSender.worldScale.y, Is.GreaterThan(0));
            }
        }

        [TestCase(true, 0f)]
        [TestCase(true, 0.75f)]
        [TestCase(true, 2f)]
        [TestCase(false, 1f)]
        [TestCase(false, 1.5f)]
        [TestCase(false, 2f)]
        public void ZeroRadiusSlicesKeepSenderTransformInvertible(bool emptyProfile, float distance) {
            if (emptyProfile) bake.radiusCurve = AnimationCurve.Constant(0, 1, 0);
            Start();
            SetMergedProximity( Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            foreach (var scale in new[] { 0.1f, 1f, 3f }) {
                root.transform.localScale = Vector3.one * (2 * scale);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
                var senderScale = bake.mainSender.worldScale;
                Assert.That(senderScale.x, Is.GreaterThan(0));
                Assert.That(senderScale.y, Is.GreaterThan(0));
                Assert.That(senderScale.z, Is.GreaterThan(0));
                Assert.That(senderScale.y, Is.EqualTo(MinimumRadius * scale).Within(0.00000001f));
                var matrix = bake.mainSender.localToWorldMatrix;
                Assert.That(matrix.determinant, Is.GreaterThan(0));
                var inverse = matrix.inverse;
                for (var i = 0; i < 16; i++) Assert.That(float.IsNaN(inverse[i]) || float.IsInfinity(inverse[i]), Is.False);
                Assert.That(bake.mainSender.GetComponent<VRCContactSender>().radius
                    * Mathf.Max(senderScale.x, senderScale.y, senderScale.z), Is.EqualTo(0.4f * scale).Within(0.00001f));
            }
        }

        [Test]
        public void ReciprocalDoesNotBlendInItsNonzeroParameterDefault() {
            var tree = VFBlendTreeDirect.Create("Reciprocal regression");
            fx.NewLayer("Reciprocal regression").NewState("DBT").WithAnimation(tree);
            var input = fx.CreateUniqueFloat("Reciprocal input", def: 2);
            var reciprocal = new BlendtreeMath(fx, tree).Invert("Reciprocal output", input);
            Start();
            foreach (var value in new[] { 2f, 4f, 0.5f }) {
                playable.SetFloat(input.name, value);
                for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                Assert.That(playable.GetFloat(reciprocal.name), Is.EqualTo(1 / value).Within(0.0001f));
            }
        }

        [Test]
        public void ManualPlugWithoutMeshUsesConstantRadius() {
            plug.configureTpsMesh.Clear();
            var injector = new VRCFuryInjector(); injector.ImportScan(typeof(VFServiceAttribute));
            bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            foreach (var depth in new[] { 0f, 0.25f, 0.75f, 1f })
                Assert.That(bake.radiusCurve.Evaluate(depth), Is.EqualTo(0.06f).Within(0.00001f));
        }

#if VRCSDK_HAS_MATERIAL_CONTACTS
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
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(contacts, Has.Length.EqualTo(5)); // Quartet plus shader-only path buffer.
            Assert.That(contacts.All(c => c.transform.parent == (Transform)bake.bakeRoot), Is.True);
            Assert.That(contacts.Select(c => c.name), Is.EquivalentTo(new[] {
                "Receiver - Sockets (Self)", "Receiver - Sockets (Others)", "Receiver - Socket Paths",
                "Receiver - Sockets (Self Merged)", "Receiver - Sockets (Others Merged)"
            }));
            var r = bake.socketReceivers;
            Assert.That(SourceReceiver(true).writeToMaterials && SourceReceiver(false).writeToMaterials, Is.True);
            Assert.That(MergedReceiver(true).writeToMaterials || MergedReceiver(false).writeToMaterials, Is.False);
            Assert.That(SourceReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.EqualTo(MergedReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(true).parameter).And.Not.EqualTo(SourceReceiver(false).parameter));
            var again = receiverAnimator.GetParameter(r, Party.Merged);
            Assert.That(again.name, Is.EqualTo(MergedReceiver(true).parameter));
        }
#endif

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
                playable.SetFloat(SourceReceiver(true).parameter + "_PointSeparation", values.Item1 == 0 ? -1 : 1 - values.Item1);
                playable.SetFloat(SourceReceiver(false).parameter + "_PointSeparation", values.Item2 == 0 ? -1 : 1 - values.Item2);
                // SDK output deliberately differs from both selector inputs. Sampling must use
                // this current merged value immediately, rather than max/self/other from a DBT.
                SetMergedProximity( 0.6f);
                graph.Evaluate(1f / 60);
                Assert.That(SourceReceiver(true).gameObject.activeSelf && SourceReceiver(false).gameObject.activeSelf, Is.True);
                Assert.That(MergedReceiver(true).enabled && MergedReceiver(false).enabled, Is.True);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item3));
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(!values.Item3));
                Assert.That(playable.IsParameterControlledByCurve(MergedReceiver(true).parameter), Is.False);
                for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(0.0216f + MinimumRadius * 0.64f).Within(0.00002f));
            }
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [Test]
        public void MergedSelectionUsesOnlyPointSeparationWithoutBuffering() {
            Start();
            var self = SourceReceiver(true).parameter;
            var others = SourceReceiver(false).parameter;
            foreach (var values in new[] {
                         (1f, 1f, 0.1f, 0.3f, true),
                         (1f, 1f, 0.3f, 0.1f, false),
                         (1f, 1f, 0.2f, 0.2f, false),
                         (1f, 1f, 0f, 0.1f, true),
                         (1f, 0.9f, 0.3f, 0.1f, false),
                         (0.9f, 1f, 0.1f, 0.3f, true),
                         (0.9f, 0.1f, -1f, 0.3f, false),
                         (0.1f, 0.9f, 0.1f, -1f, true),
                         (0f, 1f, 0f, -1f, true),
                         (1f, 0f, -1f, 0f, false),
                         (1f, 1f, 0f, 0f, false),
                         (0f, 0f, -1f, -1f, false),
                         (1f, 1f, 0.1f, 0.3f, true),
                         (VRCFuryEditorUtils.NextFloatDown(1), 1f, 0.1f, 0.3f, true),
                         (1f, VRCFuryEditorUtils.NextFloatDown(1), 0.3f, 0.1f, false)
                     }) {
                playable.SetFloat(self, values.Item1);
                playable.SetFloat(others, values.Item2);
                playable.SetFloat(self + "_PointSeparation", values.Item3);
                playable.SetFloat(others + "_PointSeparation", values.Item4);
                SetMergedProximity( 0.6f);
                graph.Evaluate(1f / 60);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item5), values.ToString());
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(!values.Item5), values.ToString());
                for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(0.0216f + MinimumRadius * 0.64f).Within(0.00002f));
            }
        }
#endif

#if VRCSDK_HAS_MATERIAL_CONTACTS
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
#endif

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        // This integration probe calls the SDK 3.10.6 collision-job internals directly.
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
                    receiver.param_PointSeparation = new ParameterAccess(playable, receiver.parameter + "_PointSeparation");
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
                    self.transform.position = bake.bakeRoot.TransformPoint(Vector3.forward * (bake.localLength * distances.Item1));
                    other.transform.position = bake.bakeRoot.TransformPoint(Vector3.forward * (bake.localLength * distances.Item2));
                    Tick();
                    Tick();
                    Assert.That(playable.GetFloat(SourceReceiver(true).parameter),
                        Is.EqualTo(1 - distances.Item1 / SpsPlugReceiverService.RangeInPlugLengths).Within(0.0001f),
                        "self source; usage=" + SourceReceiver(true).Usage + "/" + self.Usage
                        + " player=" + Call(SourceReceiver(true), "GetPlayerId") + "/" + Call(self, "GetPlayerId")
                        + " raw=" + SourceReceiver(true).paramValue
                        + " records=" + ((System.Collections.ICollection)Get(SourceReceiver(true), "collisionRecords")).Count);
                    Assert.That(playable.GetFloat(SourceReceiver(false).parameter),
                        Is.EqualTo(1 - distances.Item2 / SpsPlugReceiverService.RangeInPlugLengths).Within(0.0001f), "others source");
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
                    var expected = 1 - Mathf.Min(distances.Item1, distances.Item2) / SpsPlugReceiverService.RangeInPlugLengths;
                    Assert.That(playable.GetFloat(MergedReceiver(true).parameter), Is.EqualTo(expected).Within(0.0001f),
                        "self active=" + MergedReceiver(true).gameObject.activeSelf + " others active=" + MergedReceiver(false).gameObject.activeSelf);
                    for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
                    Assert.That(bake.mainSender.worldScale.y,
                        Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, Mathf.Min(distances.Item1, distances.Item2))).Within(0.0001f));
                }
            } finally {
                // Contact destruction must happen while its manager and parameter accesses are valid.
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObj);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
        }
#endif
    }
}
