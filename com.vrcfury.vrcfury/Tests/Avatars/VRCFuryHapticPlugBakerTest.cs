namespace VF.Tests {
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
    using VRC.Dynamics;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;

    internal class VRCFuryHapticPlugBakerTest : SpsPlugReceiverTestBase {
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
            Assert.That(bake.worldLength, Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(bake.worldRadius, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(bake.socketReceivers, Is.SameAs(receivers));
            Assert.That(bake.bakeRoot, Is.EqualTo(frame));
            // Rotating the hip frame introduces normal transform-decomposition rounding.
            Assert.That(Vector3.Distance(frame.worldScale, Vector3.one), Is.LessThan(0.000001f));
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
            Assert.That(bake.localLength, Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(bake.localRadius, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(keys.First().time, Is.Zero);
            Assert.That(keys.Last().time, Is.EqualTo(1));
            Assert.That(keys.First().value, Is.EqualTo(bake.localRadius));
            root.transform.localScale *= resize;
            Assert.That(bake.worldLength, Is.EqualTo(0.4f * resize).Within(0.00001f));
            Assert.That(bake.worldRadius, Is.EqualTo(0.01f * resize).Within(0.00001f));
            Assert.That(bake.localLength, Is.EqualTo(0.4f).Within(0.000001f));
            Assert.That(bake.localRadius, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(bake.localMinimumRadius, Is.EqualTo(minimumRadius));
            Assert.That(bake.radiusCurve.keys, Is.EqualTo(keys));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.00001f));
            Assert.That(bake.mainSender.worldScale.x, Is.EqualTo(bake.worldRadius).Within(0.00001f));
        }

        [Test]
        public void BakeDoesNotReuseMutableMaterialsAcrossRenderers(
            [Values] bool compatibility,
            [Values] bool deferred
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
                Assert.That(first.HasProperty("_SPS_Overrun"), Is.False);
                Assert.That(second.HasProperty("_SPS_Overrun"), Is.False);
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

    internal class VRCFuryHapticPlugBakerSenderTest : SpsSenderBakeTestBase {
        [Test]
        public void PlugBakerReusesLengthSenderAndPreservesLegacyGeometry() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(bake.channelContacts);
            var contacts = root.GetComponentsInChildren<VRCContactSender>(true);

            Assert.That(contacts.Length, Is.EqualTo(4));
            CheckVector(bake.bakeRoot.worldScale, Vector3.one);
            Assert.That(contacts.All(c => c.transform.parent == (Transform)bake.bakeRoot), Is.True);
            Assert.That(contacts.Select(c => c.name), Is.EquivalentTo(new[] {
                "Sender - Sphere", "Sender - Legacy Width", "Sender - Legacy Root", "Sender - Envelope"
            }));
            var main = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugSphere));
            var width = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugWidth));
            var envelope = contacts.Single(c => c.collisionTags.Contains(SpsTags.PlugClose));
            var legacyRoot = contacts.Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugRoot));
            Assert.That(main.collisionTags, Does.Contain(SpsTags.FormatPlug("_public")));
            Assert.That(main.collisionTags.Any(tag => tag.StartsWith("SPS/Flag/")), Is.False);
            Assert.That(envelope.collisionTags, Is.EqualTo(new[] { SpsTags.PlugClose }));
            foreach (var tag in new[] { SpsTags.LegacyPlugWidth,
                         SpsTags.LegacyPlugRoot }) {
                Assert.That(contacts.Single(c => c.collisionTags.Contains(tag)).collisionTags,
                    Is.EquivalentTo(new[] { tag }));
            }
            Assert.That(contacts.SelectMany(c => c.collisionTags).Any(t => t.StartsWith("SPSN1/Legacy")), Is.False);
            Assert.That(main.radius * MaxScale(main.transform), Is.EqualTo(0.4f).Within(0.00001));
            Assert.That(width.radius * MaxScale(width.transform), Is.EqualTo(0.34f).Within(0.00001));
            Assert.That(envelope.radius * MaxScale(envelope.transform), Is.EqualTo(0.03f).Within(0.00001));
            Assert.That(legacyRoot.radius * MaxScale(legacyRoot.transform), Is.Zero.Within(0.00001));
            Assert.That(envelope.position.z * envelope.transform.lossyScale.z, Is.EqualTo(0.2f).Within(0.00001));

            var native = main;
            Assert.That(native.radius, Is.EqualTo(1).Within(0.000001));
            Assert.That(native.collisionTags, Does.Not.Contain(SpsTags.LegacyPlugSphere + "_SelfNotOnHips"));
            Assert.That(native.collisionTags.Any(t => t.StartsWith("SPSN1/Plug/")), Is.False);
            Assert.That(native.position, Is.EqualTo(Vector3.zero));
            CheckVector(native.transform.lossyScale, new Vector3(0.03f, 0.03f, 0.4f));
            Assert.That(VFConstraint.GetConstraintsInSelfAndChildren(new[] { native.owner() }), Is.Empty);
            CheckVector(native.transform.position, root.transform.position);
            CheckVector(native.transform.forward, root.transform.forward);
            root.transform.localScale *= 3;
            Assert.That(native.transform.lossyScale.x, Is.EqualTo(0.09f).Within(0.00001));
            Assert.That(native.transform.lossyScale.y, Is.EqualTo(0.09f).Within(0.00001));
            Assert.That(native.transform.lossyScale.z, Is.EqualTo(1.2f).Within(0.00001));
            Assert.That(main.radius * MaxScale(main.transform), Is.EqualTo(1.2f).Within(0.00001));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false)]
        public void PublicPlugRouteKeepsLegacyTpsTagAndHipPolicy(bool onHips, bool avoidHips) {
            if (!BuildTargetUtils.IsDesktop()) return;
            injector.Set("autoChannelGenerator", new AutoChannels(onHips ? HumanBodyBones.Hips : HumanBodyBones.LeftHand));
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.channelSettings.useHipAvoidance = avoidHips;
            injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            injector.GetService<SpsChannelsService>().Apply(injector.GetService<SpsChannelsService>().Get(plug));
            var sender = root.GetComponentsInChildren<VRCContactSender>()
                .Single(c => c.collisionTags.Contains("TPS_Pen_Penetrating"));
            Assert.That(sender.collisionTags.Contains("TPS_Pen_Penetrating_SelfNotOnHips"),
                Is.False);
            Assert.That(sender.collisionTags, Does.Contain("SPS/Plug/_public"));
            Assert.That(sender.collisionTags.Contains(SelfTag("SPS/Plug/nothips")),
                Is.EqualTo(onHips && avoidHips));
            Assert.That(sender.collisionTags, Does.Not.Contain("SPSN1/LegacyPlug/Main"));
            Assert.That(sender.collisionTags, Does.Not.Contain("SPSN1/LegacyPlug/Main_SelfNotOnHips"));
            Assert.That(sender.collisionTags.Any(t => t.StartsWith("SPSN1/Plug/")), Is.False);
            Assert.That(sender.collisionTags.Any(t => t.Contains("SCALE_DATA")), Is.False);
            Assert.That(sender.collisionTags.Where(t => t.StartsWith("SPS/Plug/")), Is.EquivalentTo(
                !onHips || !avoidHips
                    ? new[] { "SPS/Plug/_public", SelfTag("SPS/Plug/_public") }
                    : new[] { "SPS/Plug/_public", SelfTag("SPS/Plug/nothips") }));
            CheckVector(sender.transform.lossyScale, new Vector3(0.03f, 0.03f, 0.4f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeformingPlugKeepsItsBakeWithoutCreatingMarkers(bool withMaterial) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(-0.015f, 0, 0), new Vector3(0.015f, 0, 0),
                    new Vector3(-0.015f, 0, 0.2f), new Vector3(0.015f, 0, 0.2f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }
            };
            mesh.RecalculateNormals();
            Material originalMaterial = null;
            try {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                if (withMaterial) originalMaterial = new Material(Shader.Find("Unlit/Color"));
                renderer.sharedMaterials = withMaterial ? new[] { originalMaterial } : Array.Empty<Material>();
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.spsAutorig = false;
                plug.configureTpsMesh.Add(renderer);
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.03f;
#if !VRCSDK_HAS_MATERIAL_CONTACTS
                if (BuildTargetUtils.IsDesktop()) {
                    var error = Assert.Throws<Exception>(() => injector.GetService<VRCFuryHapticPlugBaker>()
                        .Bake(plug, deferMaterialConfig: !withMaterial));
                    Assert.That(error.Message, Is.EqualTo(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired));
                    return;
                }
#endif
                var result = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, deferMaterialConfig: !withMaterial);
                injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
                Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.EquivalentTo(new[] { renderer }));
                Assert.That(result.bakeRoot.GetComponentsInSelfAndChildren<Animator>(), Is.Empty);
                if (!withMaterial) Assert.That(renderer.sharedMaterials, Is.Empty);
                if (BuildTargetUtils.IsDesktop()) {
                    if (withMaterial) {
                        Assert.That(renderer.sharedMaterial, Is.Not.SameAs(originalMaterial));
                        Assert.That(renderer.sharedMaterial.GetTexture("_SPS_Bake"), Is.Not.Null);
                        Assert.That(renderer.sharedMaterial.GetFloat("_SPS_BakedLength"), Is.EqualTo(0.4f));
                        Assert.That(renderer.sharedMaterial.GetFloat("_SPS_BakedRadius"), Is.EqualTo(0.03f));
                        Assert.That(renderer.sharedMaterial.HasProperty("_SPS_IdLow"), Is.False);
                    }
                    var sender = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                        .Single(c => c.collisionTags.Contains(SpsTags.LegacyPlugSphere));
                    CheckVector(sender.transform.lossyScale, new Vector3(0.03f, 0.03f, 0.4f));
                    var receivers = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>();
                    Assert.That(receivers.Length, Is.EqualTo(3));
                    foreach (var receiver in receivers) {
                        var parent = receiver.name == "Receiver - Socket Paths" ? result.pathReceiverRoot : result.bakeRoot;
                        Assert.That(receiver.transform.parent, Is.EqualTo((Transform)parent));
                        CheckVector(receiver.transform.lossyScale, Vector3.one);
                        CheckVector(receiver.transform.position, result.bakeRoot.worldPosition);
                    }
                    root.transform.localScale *= 3;
                    foreach (var receiver in receivers) {
                        CheckVector(receiver.transform.lossyScale, Vector3.one * 3);
                    }
                }
            } finally {
                if (originalMaterial != null) Object.DestroyImmediate(originalMaterial);
                Object.DestroyImmediate(mesh);
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

    internal class VRCFuryHapticPlugBakerChannelValidationTest : SpsChannelBuildTestBase {
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void PlugWithNoChannelsRejectsBaking() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var plug = Plug(obj, false);
            plug.channelSettings.channels.Clear();
            Assert.Throws<System.Exception>(() => BakePlug(plug));
        }
#endif
    }
}
