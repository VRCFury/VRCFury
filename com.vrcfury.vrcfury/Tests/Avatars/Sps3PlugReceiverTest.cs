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

        private class HipsChannels : SpsAutoChannelGenerator {
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => HumanBodyBones.Hips;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

        [Test]
        public void ReceiverAndBakerShareCachedSizeAndFramesRegardlessOfRequestOrder() {
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
            if (!BuildTargetUtils.IsDesktop()) {
                Assert.That(bake.mainSender, Is.Null);
                return;
            }
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.00001f));
            Assert.That(bake.mainSender.worldScale.x, Is.EqualTo(bake.worldRadius).Within(0.00001f));
        }

        [Test]
        public void SeparateChannelListsAndLegacyTagOrder() {
            plug.useSharedTag = plug.useHipAvoidance = true;
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "first", allowSelf = true, allowOthers = false });
            plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "second", allowSelf = false, allowOthers = true });
            Assert.That(service.GetSocketTags(plug, true), Is.EqualTo(new[] {
                SpsTags.FormatSocket(SpsTags.NotHipsChannel), SpsTags.FormatSocket("first")
            }));
            Assert.That(service.GetSocketTags(plug, false), Is.EqualTo(new[] {
                SpsTags.FormatSocket(SpsTags.PublicChannel), SpsTags.FormatSocket("second"),
                SpsTags.LegacySocketHole, SpsTags.LegacySocketRing
            }));
            Assert.That(service.GetPathTags(plug), Is.EquivalentTo(new[] {
                SpsTags.FormatSocketPath(SpsTags.PublicChannel), SpsTags.FormatSocketPath(SpsTags.NotHipsChannel),
                SpsTags.FormatSocketPath("first"), SpsTags.FormatSocketPath("second")
            }));
        }

        [Test]
        public void PrivateChannelsDoNotSubscribeToLegacyMetadataOrLights() {
            plug.useSharedTag = false;
            plug.includeTags.Clear();
            Assert.That(service.GetSocketTags(plug, true), Is.Empty);
            Assert.That(service.GetSocketTags(plug, false), Is.Empty);
            Assert.That(service.GetPathTags(plug), Is.Empty);
        }

#if VRCSDK_HAS_MATERIAL_CONTACTS
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
            var reserved = root.GetComponentInChildren<VRCContactReceiver>();
            // Even an inactive material output with room must not lose its assigned shader parameter.
            reserved.writeToMaterials = false;
            receivers.SetMaterials(new[] { renderer, renderer }, self, needsTags, "Requested");
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(all, Has.Length.EqualTo(2));
            var writer = all.Single(r => r.separateShaderParameter == "Requested");
            Assert.That(reserved.separateShaderParameter, Is.EqualTo("Reserved"));
            Assert.That(writer.allowSelf, Is.EqualTo(self));
            Assert.That(writer.allowOthers, Is.EqualTo(!self));
            Assert.That(writer.writeToMaterialsContactTags, Is.EqualTo(needsTags));
            Assert.That(writer.shaderTargetRenderers, Is.EqualTo(new[] { renderer }));
            receivers.SetMaterials(new[] { renderer }, self, true, "Requested");
            receivers.SetMaterials(new[] { renderer }, self, false, "Requested");
            Assert.That(writer.writeToMaterialsContactTags, Is.True);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(2));
            receivers.SetMaterials(new[] { renderer }, self, needsTags, "Another");
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(3));
            Assert.That(writer.separateShaderParameter, Is.EqualTo("Requested"));
        }

        [Test]
        public void SdkWritesCanonicalFrameToSeparateMaterialParameterEvenWithNoSenders() {
            Assert.That(ContactReceiver.SHADER_MAX_TARGET_CONTACTS, Is.EqualTo(8));
            var reference = GameObjects.Create("OGBTestBase", root);
            reference.localPosition = new Vector3(0.3f, 0.5f, -0.1f);
            reference.localRotation = Quaternion.Euler(17, 29, 41);
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            var renderer = GameObjects.Create("Renderer", root).AddComponent<MeshRenderer>();
            service.Create(plug, new[] { renderer });
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>().Where(r => r.writeToMaterials).ToArray();
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
                    ? service.GetPathTags(plug) : service.GetSocketTags(plug, receiver.separateShaderParameter == "SPS_Self")));
            }
        }
#else
        [Test]
        public void RejectsSdksWithoutMaterialContacts() {
            var error = Assert.Throws<Exception>(() => service.Create(plug, Array.Empty<Renderer>()));
            Assert.That(error.Message, Does.Contain("Update the VRCSDK"));
        }
#endif

        [Test]
        public void MaterialSettingsUseOthersChannelCountAndLegacySwitches() {
            var material = new Material(Shader.Find("Unlit/Color"));
            try {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
                Assert.That(Assert.Throws<Exception>(() => SpsPatcher.Patch(material, false, false)).Message,
                    Does.Contain("Update the VRCSDK"));
#else
                SpsPatcher.Patch(material, false, false);
                service.ConfigureMaterial(material, plug);
                Assert.That(material.GetFloat("_SPS_OthersChannelTagCount"), Is.EqualTo(1));
                Assert.That(material.GetFloat("_SPS_OthersLegacyTags"), Is.EqualTo(1));
                Assert.That(material.GetFloat("_SPS_UseLegacyLights"), Is.EqualTo(1));
                Assert.That(material.HasProperty("_SPS_BakeRootScale"), Is.False);
                Assert.That(material.HasProperty("_SPS_PlugOrigin"), Is.False);
                plug.useLights = false;
                service.ConfigureMaterial(material, plug);
                Assert.That(material.GetFloat("_SPS_UseLegacyLights"), Is.EqualTo(1));
                plug.useSharedTag = false;
                plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "self", allowSelf = true, allowOthers = false });
                plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "both", allowSelf = true, allowOthers = true });
                service.ConfigureMaterial(material, plug);
                Assert.That(service.GetSocketTags(plug, true).Count, Is.EqualTo(2));
                Assert.That(material.GetFloat("_SPS_OthersChannelTagCount"), Is.EqualTo(1));
                Assert.That(material.GetFloat("_SPS_OthersLegacyTags"), Is.Zero);
#endif
            } finally {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void BakeDoesNotReuseMutableMaterialsAcrossRenderers() {
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
                injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
                if (!BuildTargetUtils.IsDesktop()) return;
                var first = plug.configureTpsMesh[0].sharedMaterial;
                var second = plug.configureTpsMesh[1].sharedMaterial;
                Assert.That(first, Is.Not.SameAs(source));
                Assert.That(second, Is.Not.SameAs(source));
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.GetTexture("_SPS_Bake"), Is.Not.SameAs(second.GetTexture("_SPS_Bake")));
                Assert.That(source.shader, Is.EqualTo(Shader.Find("Unlit/Color")));
            } finally {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(source);
            }
        }
    }
}
