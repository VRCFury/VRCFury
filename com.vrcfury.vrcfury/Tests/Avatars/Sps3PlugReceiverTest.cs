using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class Sps3PlugReceiverTest {
        private GameObject root;
        private VRCFuryHapticPlug plug;
        private VRCFuryInjector injector;

        private string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        private SpsPlugReceiverService service;

        [SetUp]
        public void SetUp() {
            root = new GameObject("SPS3 Receiver Test");
            root.transform.SetPositionAndRotation(new Vector3(12, -7, 4), Quaternion.Euler(31, 47, 13));
            root.transform.localScale = Vector3.one * 2;
            plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.01f;
            injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            injector.Set("autoChannelGenerator", new HipsChannels());
            service = injector.GetService<SpsPlugReceiverService>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private IEnumerable<string> SocketTags(bool self) {
            var channels = injector.GetService<SpsChannelsService>();
            var main = service.GetReceivers(plug);
            return main.request.getTags(channels.Get(plug).Default, self);
        }

        private IEnumerable<string> PathTags() {
            var channels = injector.GetService<SpsChannelsService>();
            return channels.Get(plug).Default.paths.Select(SpsTags.FormatSocketPath);
        }

        private void FinalizeChannels() {
            var channels = injector.GetService<SpsChannelsService>();
            channels.Apply(channels.Get(plug));
        }

        private class HipsChannels : SpsAutoChannelGenerator {
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => HumanBodyBones.Hips;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

        [Test]
        public void ReceiverAndBakerShareCachedSizeAndFramesRegardlessOfRequestOrder() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            plug.enableSps = false;
            var sizes = injector.GetService<PlugSizeService>();
            var size = sizes.GetWorldSize(plug);
            var receivers = service.GetReceivers(plug);
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            plug.length = 0.8f;
            plug.radius = 0.02f;
            Assert.That(sizes.GetWorldSize(plug), Is.SameAs(size));
            Assert.That(receivers.IsCreated, Is.False);
            var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            Assert.That(bake.worldLength, Is.EqualTo(0.4f));
            Assert.That(bake.worldRadius, Is.EqualTo(0.01f));
            Assert.That(bake.socketReceivers, Is.SameAs(receivers));
            Assert.That(bake.bakeRoot, Is.EqualTo(frame));
            Assert.That(frame.worldScale, Is.EqualTo(Vector3.one));
            Assert.That(root.GetComponentsInChildren<Transform>().Count(t => t.name == "BakedSpsPlug"), Is.EqualTo(1));
            Assert.That(receivers.IsCreated, Is.False);
            var another = GameObjects.Create("Other plug", root).AddComponent<VRCFuryHapticPlug>();
            another.autoRenderer = another.autoLength = another.autoRadius = false;
            another.unitsInMeters = true;
            another.length = 0.7f;
            another.radius = 0.03f;
            Assert.That(sizes.GetWorldSize(another).worldLength, Is.EqualTo(0.7f));
            Assert.That(sizes.GetWorldSize(another), Is.Not.SameAs(size));
        }

        [TestCase(0.5f, 0.5f)]
        [TestCase(0.5f, 3f)]
        [TestCase(2f, 0.5f)]
        [TestCase(2f, 3f)]
        [TestCase(3f, 0.5f)]
        [TestCase(3f, 3f)]
        public void BakeResultKeepsLocalGeometryAndReportsCurrentWorldDimensions(float uploadScale, float resize) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            root.transform.localScale = Vector3.one * uploadScale;
            plug.enableSps = false;
            var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            var keys = bake.radiusCurve.keys;
            var minimumRadius = bake.localMinimumRadius;
            Assert.That(bake.localLength, Is.EqualTo(0.4f));
            Assert.That(bake.localRadius, Is.EqualTo(0.01f));
            Assert.That(keys.First().time, Is.Zero);
            Assert.That(keys.Last().time, Is.EqualTo(1));
            Assert.That(keys.First().value, Is.EqualTo(bake.localRadius));
            root.transform.localScale *= resize;
            Assert.That(bake.worldLength, Is.EqualTo(0.4f * resize).Within(0.00001f));
            Assert.That(bake.worldRadius, Is.EqualTo(0.01f * resize).Within(0.00001f));
            Assert.That(bake.localLength, Is.EqualTo(0.4f));
            Assert.That(bake.localRadius, Is.EqualTo(0.01f));
            Assert.That(bake.localMinimumRadius, Is.EqualTo(minimumRadius));
            Assert.That(bake.radiusCurve.keys, Is.EqualTo(keys));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.00001f));
            Assert.That(bake.mainSender.worldScale.x, Is.EqualTo(bake.worldRadius).Within(0.00001f));
        }

        [Test]
        public void SeparateChannelListsAndLegacyTagOrder() {
            plug.channelSettings.useSharedTag = plug.channelSettings.useHipAvoidance = true;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "first", allowSelf = true, allowOthers = false });
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "second", allowSelf = false, allowOthers = true });
            Assert.That(SocketTags(true), Is.EqualTo(new[] {
                SpsTags.FormatSocket(SelfTag("first")), SpsTags.FormatSocket(SelfTag(SpsTags.NotHipsChannel))
            }));
            Assert.That(SocketTags(false), Is.EqualTo(new[] {
                SpsTags.FormatSocket(SpsTags.PublicChannel), SpsTags.FormatSocket("second"),
                SpsTags.LegacySocketHole, SpsTags.LegacySocketRing, SpsTags.LegacySocket
            }));
            Assert.That(PathTags(), Is.EquivalentTo(new[] {
                SpsTags.FormatSocketPath(SpsTags.PublicChannel), SpsTags.FormatSocketPath(SelfTag(SpsTags.NotHipsChannel)),
                SpsTags.FormatSocketPath(SelfTag("first")), SpsTags.FormatSocketPath("second")
            }));
        }

        [Test]
        public void PrivateChannelsDoNotSubscribeToLegacyMetadataOrLights() {
            plug.channelSettings.useSharedTag = false;
            plug.channelSettings.channels.Clear();
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "private" });
            Assert.That(SocketTags(true), Is.EqualTo(new[] { SpsTags.FormatSocket(SelfTag("private")) }));
            Assert.That(SocketTags(false), Is.EqualTo(new[] { SpsTags.FormatSocket("private") }));
            Assert.That(PathTags(), Is.EquivalentTo(new[] { SpsTags.FormatSocketPath("private"), SpsTags.FormatSocketPath(SelfTag("private")) }));
        }

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void FinalizationDoesNotModifyUnconfiguredSharedMaterials() {
            var material = new Material(Shader.Find("Unlit/Color"));
            try {
                SpsPatcher.Patch(material, false, false);
                material.SetFloat("_SPS_OthersChannelTagCount", 73);
                material.SetFloat("_SPS_OthersLegacyTags", 74);
                material.SetFloat("_SPS_UseLegacyLights", 75);
                var renderer = GameObjects.Create("Unconfigured material slot", root).AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var original = UnityEditor.EditorJsonUtility.ToJson(material);
                service.Create(plug, new[] { renderer });
                FinalizeChannels();
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(UnityEditor.EditorJsonUtility.ToJson(material), Is.EqualTo(original),
                    "Only the baker's configured SPS copies may be modified, not other shared material slots");
            } finally { Object.DestroyImmediate(material); }
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void MaterialRequestsMatchShaderParameterAndRespectExplicitPartyAndTags(bool self, bool needsTags) {
            var receivers = service.GetReceivers(plug);
            receivers.SetMaterials(Array.Empty<Renderer>(), self, needsTags, "Requested");
            Assert.That(receivers.IsCreated, Is.False);
            var renderer = GameObjects.Create("Material target", root).AddComponent<MeshRenderer>();
            receivers.SetMaterials(new[] { renderer }, self, false, "Reserved");
            var reservedRequest = receivers.Find(self, false);
            // Even an inactive material output with room must not lose its assigned shader parameter.
            reservedRequest.shaderTargetRenderers.Clear();
            receivers.SetMaterials(new[] { renderer, renderer }, self, needsTags, "Requested");
            receivers.SetMaterials(new[] { renderer }, self, true, "Requested");
            receivers.SetMaterials(new[] { renderer }, self, false, "Requested");
            receivers.SetMaterials(new[] { renderer }, self, needsTags, "Another");
            FinalizeChannels();
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(all, Has.Length.EqualTo(3));
            var reserved = reservedRequest.component;
            var writer = all.Single(r => r.separateShaderParameter == "Requested");
            Assert.That(reserved.separateShaderParameter, Is.EqualTo("Reserved"));
            Assert.That(writer.allowSelf, Is.EqualTo(self));
            Assert.That(writer.allowOthers, Is.EqualTo(!self));
            Assert.That(writer.writeToMaterialsContactTags, Is.True);
            Assert.That(writer.shaderTargetRenderers, Is.EqualTo(new[] { renderer }));
            Assert.That(writer.separateShaderParameter, Is.EqualTo("Requested"));
        }

        [Test]
        public void SdkWritesCanonicalFrameToSeparateMaterialParameterEvenWithNoSenders() {
            Assert.That(ContactReceiver.SHADER_MAX_TARGET_CONTACTS, Is.EqualTo(8));
            plug.position = new Vector3(0.3f, 0.5f, -0.1f);
            plug.rotation = new Vector3(17, 29, 41);
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            var renderer = GameObjects.Create("Renderer", root).AddComponent<MeshRenderer>();
            service.Create(plug, new[] { renderer });
            FinalizeChannels();
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>().Where(r => r.writeToMaterials).ToArray();
            Assert.That(receivers, Has.Length.EqualTo(3));
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(ContactReceiver);
            foreach (var receiver in receivers) {
                var transformProperty = (string)type.GetProperty("ShaderPropertyNameTransforms", flags).GetValue(receiver);
                var dataProperty = (string)type.GetProperty("ShaderPropertyNameData", flags).GetValue(receiver);
                Assert.That(transformProperty, Is.EqualTo("_VRChatContact_Transforms_" + receiver.separateShaderParameter));
                Assert.That(dataProperty, Is.EqualTo("_VRChatContact_Data_" + receiver.separateShaderParameter));
                // Exercise the SDK's real material writer without starting a live global ContactManager.
                type.GetField("shaderPropIdTransforms", flags).SetValue(receiver, Shader.PropertyToID(transformProperty));
                type.GetField("shaderPropIdData", flags).SetValue(receiver, Shader.PropertyToID(dataProperty));
                type.GetField("shaderClampedRendererLength", flags).SetValue(receiver, 1);
                var update = type.GetMethod("UpdateMaterials", flags);
                update.Invoke(receiver, new[] { Activator.CreateInstance(update.GetParameters()[0].ParameterType) });
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var matrices = block.GetMatrixArray(transformProperty);
                Assert.That(matrices.Length, Is.EqualTo(9));
                Assert.That(matrices[8], Is.EqualTo(receiver.transform.localToWorldMatrix));
                Assert.That(matrices[8], Is.EqualTo(frame.localToWorldMatrix));
                Assert.That(block.GetVectorArray(dataProperty)[8].y, Is.Zero);
            }
            // Each writer preserves the other receivers' buffers on the same renderer.
            var combined = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(combined);
            foreach (var receiver in receivers) {
                Assert.That(combined.GetMatrixArray("_VRChatContact_Transforms_" + receiver.separateShaderParameter).Length,
                    Is.EqualTo(9));
            }
        }

        [TestCase(1, 3, 0.02f)]
        [TestCase(4, 3, 0.4f)]
        [TestCase(5, 6, 2f)]
        [TestCase(9, 9, 0.4f)]
        public void MaterialReceiversBatchActualRenderersWithoutAnimatorParameters(int rendererCount, int receiverCount, float worldLength) {
            plug.length = worldLength;
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            var renderers = Enumerable.Range(0, rendererCount)
                .Select(i => (Renderer)GameObjects.Create("Renderer " + i, root).AddComponent<MeshRenderer>()).ToArray();
            service.Create(plug, renderers.Concat(renderers));
            FinalizeChannels();
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>().Where(r => r.writeToMaterials).ToArray();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true).Length, Is.EqualTo(receiverCount));
            foreach (var group in receivers.GroupBy(r => r.separateShaderParameter)) {
                Assert.That(group.SelectMany(r => r.shaderTargetRenderers), Is.EquivalentTo(renderers));
            }
            foreach (var receiver in receivers) {
                Assert.That(receiver.shaderTargetRenderers.Length, Is.LessThanOrEqualTo(ContactReceiver.SHADER_MAX_UPDATED_RENDERERS));
                Assert.That(receiver.transform.parent, Is.EqualTo((Transform)frame));
                Assert.That(receiver.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(receiver.transform.localRotation, Is.EqualTo(Quaternion.identity));
                if (receiver.separateShaderParameter == "SPS_Path") {
                    Assert.That(receiver.radius, Is.EqualTo(100));
                } else {
                    Assert.That(receiver.radius * receiver.transform.lossyScale.x,
                        Is.EqualTo(worldLength * SpsPlugReceiverService.RangeInPlugLengths).Within(0.00001));
                }
                Assert.That(receiver.parameter, Is.Empty);
                Assert.That(receiver.separateShaderParameter, Is.EqualTo("SPS_Self").Or.EqualTo("SPS_Others").Or.EqualTo("SPS_Path"));
                Assert.That(receiver.writeToMaterials, Is.True);
                Assert.That(receiver.writeToMaterialsContactTags, Is.EqualTo(receiver.separateShaderParameter == "SPS_Others"));
                Assert.That(receiver.allowSelf, Is.EqualTo(receiver.separateShaderParameter != "SPS_Others"));
                Assert.That(receiver.allowOthers, Is.EqualTo(receiver.separateShaderParameter != "SPS_Self"));
                Assert.That(receiver.receiverType, Is.EqualTo(ContactReceiver.ReceiverType.Proximity));
                Assert.That(receiver.collisionTags, Is.EqualTo(receiver.separateShaderParameter == "SPS_Path"
                    ? PathTags() : SocketTags(receiver.separateShaderParameter == "SPS_Self")));
            }
        }
#else
        [Test]
        public void RejectsSdksWithoutMaterialContacts() {
            var error = Assert.Throws<Exception>(() => service.Create(plug, Array.Empty<Renderer>()));
            Assert.That(error.Message, Is.EqualTo(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired));
        }
#endif

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void MaterialSettingsUseOthersChannelCountAndLegacySwitches(bool compatibility, bool shared) {
            var material = new Material(Shader.Find("Unlit/Color"));
            try {
                SpsPatcher.Patch(material, false, false);
                plug.channelSettings.useSharedTag = shared;
                plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "self", allowOthers = false });
                plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "both" });
                var renderer = GameObjects.Create("Mesh", root).AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                service.ConfigureMaterial(material, plug, compatibility);
                service.Create(plug, new[] { renderer }, compatibility);
                FinalizeChannels();
                Assert.That(SocketTags(true).Count(), Is.EqualTo(shared ? 3 : 2));
                Assert.That(material.GetFloat("_SPS_OthersChannelTagCount"), Is.EqualTo(shared ? 2 : 1));
                Assert.That(material.GetFloat("_SPS_OthersLegacyTags"), Is.EqualTo(compatibility && shared ? 1 : 0));
                Assert.That(material.GetFloat("_SPS_UseLegacyLights"), Is.EqualTo(compatibility && shared ? 1 : 0));
                Assert.That(material.HasProperty("_SPS_BakeRootScale"), Is.False);
                Assert.That(material.HasProperty("_SPS_PlugOrigin"), Is.False);
            } finally { Object.DestroyImmediate(material); }
        }
#endif

        [Test]
        public void BakeDoesNotReuseMutableMaterialsAcrossRenderers(
            [Values] bool compatibility,
            [Values] bool deferred,
            [Values] bool overrun
        ) {
            var mesh = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right * 0.02f, Vector3.forward * 0.2f },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            var source = VrcfObjectFactory.CreateMaterial(Shader.Find("Unlit/Color"));
            try {
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.spsAutorig = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.03f;
                plug.spsOverrun = overrun;
                for (var i = 0; i < 2; i++) {
                    var obj = GameObjects.Create("Renderer " + i, root);
                    obj.localPosition = Vector3.right * i * 0.1f;
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = obj.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = new[] { source, source };
                    plug.configureTpsMesh.Add(renderer);
                }
#if !VRCSDK_HAS_MATERIAL_CONTACTS
                if (BuildTargetUtils.IsDesktop()) {
                    Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug));
                    return;
                }
#endif
                var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug,
                    deferMaterialConfig: deferred, legacySocketCompatibility: compatibility);
                if (!BuildTargetUtils.IsDesktop()) return;
                if (deferred) {
                    foreach (var result in bake.renderers) {
                        result.renderer.sharedMaterials = result.renderer.sharedMaterials.Select((material, slot) => result.configureMaterial(slot, material)).ToArray();
                    }
                }
                FinalizeChannels();
                var first = plug.configureTpsMesh[0].sharedMaterial;
                var second = plug.configureTpsMesh[1].sharedMaterial;
                Assert.That(first, Is.Not.SameAs(source));
                Assert.That(second, Is.Not.SameAs(source));
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.GetTexture("_SPS_Bake"), Is.Not.SameAs(second.GetTexture("_SPS_Bake")));
                Assert.That(first.GetFloat("_SPS_Overrun"), Is.EqualTo(overrun ? 1 : 0));
                Assert.That(second.GetFloat("_SPS_Overrun"), Is.EqualTo(overrun ? 1 : 0));
                Assert.That(first.GetFloat("_SPS_UseLegacyLights"), Is.EqualTo(compatibility ? 1 : 0));
                Assert.That(second.GetFloat("_SPS_UseLegacyLights"), Is.EqualTo(compatibility ? 1 : 0));
                Assert.That(first.GetFloat("_SPS_OthersLegacyTags"), Is.EqualTo(compatibility ? 1 : 0));
                Assert.That(second.GetFloat("_SPS_OthersLegacyTags"), Is.EqualTo(compatibility ? 1 : 0));
#if VRCSDK_HAS_MATERIAL_CONTACTS
                foreach (var receiver in bake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()
                             .Where(r => r.separateShaderParameter == "SPS_Others")) {
                    foreach (var tag in new[] { SpsTags.LegacySocketHole, SpsTags.LegacySocketRing, SpsTags.LegacySocket }) {
                        Assert.That(receiver.collisionTags.Contains(tag), Is.EqualTo(compatibility));
                    }
                }
#endif
                Assert.That(source.shader, Is.EqualTo(Shader.Find("Unlit/Color")));
            } finally {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(source);
            }
        }
    }
}
