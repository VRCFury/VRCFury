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

    internal class SpsPlugReceiverServiceTest : SpsAvatarBuildTestBase {
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
            foreach (var distance in new[] { 0.5f, 0.2f, 0.002f, 0f }) {
                Set(others, matches ? 0.5f : 0, matches ? distance : -1, native: false);
                Evaluate();
                var expectedDepth = shared && distance > 0 ? distance - 0.4f : 100;
                Assert.That(playable.GetFloat(depth), Is.EqualTo(expectedDepth).Within(0.001f));
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
                self => receiverAnimator.GetParameter(visual.GetReceivers(plug), self ? Party.Self : Party.Others).name);
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
    }
}
#endif

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

    internal class SpsPlugReceiverMaterialTest : SpsPlugReceiverTestBase {
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
#endif
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
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
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
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(1, 3, 0.02f)]
        [TestCase(4, 3, 0.4f)]
        [TestCase(5, 6, 2f)]
        [TestCase(9, 9, 0.4f)]
        public void MaterialReceiversBatchActualRenderersWithoutAnimatorParameters(int rendererCount, int receiverCount, float worldLength) {
            plug.length = worldLength;
            var frame = injector.GetService<PlugFrameService>().Get(plug);
            var renderers = Enumerable.Range(0, rendererCount)
                .Select(i => (Renderer)GameObjects.Create("Renderer " + i, root).AddComponent<MeshRenderer>()).ToArray();
            var pathRoot = service.Create(plug, renderers.Concat(renderers));
            FinalizeChannels();
            var receivers = root.GetComponentsInChildren<VRCContactReceiver>().Where(r => r.writeToMaterials).ToArray();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true).Length, Is.EqualTo(receiverCount));
            foreach (var group in receivers.GroupBy(r => r.separateShaderParameter)) {
                Assert.That(group.SelectMany(r => r.shaderTargetRenderers), Is.EquivalentTo(renderers));
            }
            foreach (var receiver in receivers) {
                Assert.That(receiver.shaderTargetRenderers.Length, Is.LessThanOrEqualTo(ContactReceiver.SHADER_MAX_UPDATED_RENDERERS));
                var expectedParent = receiver.separateShaderParameter == "SPS_Path" ? pathRoot : frame;
                Assert.That(receiver.transform.parent, Is.EqualTo((Transform)expectedParent));
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
#endif
#if !VRCSDK_HAS_MATERIAL_CONTACTS
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
    }
}

namespace VF.Tests {
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

    internal class SpsPlugReceiverParameterRequestsTest : SpsPlugRadiusTestBase {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true)]
        [TestCase(false)]
        public void OgbReceiverSharedWithMaterialAlwaysRunsRemotely(bool shaderFirst) {
            var receivers = bake.socketReceivers;
            void AddMaterial() => receivers.SetMaterials(plug.configureTpsMesh, false, true, "SPS_Others");
            if (shaderFirst) AddMaterial();
            receiverAnimator.RegisterOgbDepth(receivers, "OGB/Plug/Test");
            if (!shaderFirst) AddMaterial();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(contacts, Has.Length.EqualTo(2));
            Assert.That(contacts.Single(r => r.parameter == "OGB/Plug/Test/DepthSelf").localOnly, Is.True);
            var others = contacts.Single(r => r.parameter == "OGB/Plug/Test/DepthOthers");
            Assert.That(others.localOnly, Is.False);
            Assert.That(others.writeToMaterials, Is.True);
        }
#endif
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void NamedOgbParameterIsReusedByDepthAndShaderConsumers(bool self, bool shaderFirst) {
            var receivers = bake.socketReceivers;
            var party = self ? Party.Self : Party.Others;
            var parameterName = "OGB/Plug/Test/" + (self ? "DepthSelf" : "DepthOthers");
            var renderer = plug.configureTpsMesh.Single();
            void Materials() => receivers.SetMaterials(new[] { renderer }, self, false, "ShaderOutput");
            if (shaderFirst) Materials();
            var ogb = receiverAnimator.GetParameter(receivers, party, parameterName, ContactReceiver.AdvancedParameter.SenderType);
            if (!shaderFirst) Materials();
            var depth = receiverAnimator.GetParameter(receivers, party, advancedParameters: ContactReceiver.AdvancedParameter.PointSeparation);
            Assert.That(ogb.name, Is.EqualTo(parameterName));
            Assert.That(depth.name, Is.EqualTo(parameterName));
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameterName), Is.Not.Null);
            receiverAnimator.GetParameter(receivers, Party.Merged);
            Assert.That(receiverAnimator.GetParameter(receivers, party, parameterName).name, Is.EqualTo(parameterName));
            receiverAnimator.GetParameter(receivers, Party.Merged);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == parameterName);
            Assert.That(receiver.parameter, Is.EqualTo(parameterName));
            Assert.That(receiver.writeToMaterials, Is.True);
            Assert.That(receiver.shaderTargetRenderers, Is.EqualTo(new[] { renderer }));
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderType
                | ContactReceiver.AdvancedParameter.PointSeparation));
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameterName + ContactReceiver.PARAM_POINT_SEPARATION), Is.Not.Null);
            Assert.That(SourceReceiver(self), Is.SameAs(receiver));
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
        }
#endif
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void DifferentExplicitParameterNamesDoNotRenameExistingReceivers() {
            var receivers = bake.socketReceivers;
            var first = receiverAnimator.GetParameter(receivers, Party.Self, "OGB/First");
            var second = receiverAnimator.GetParameter(receivers, Party.Self, "OGB/Second");
            Assert.That(first.name, Is.EqualTo("OGB/First"));
            Assert.That(second.name, Is.EqualTo("OGB/Second"));
            Assert.That(receiverAnimator.GetParameter(receivers, Party.Self).name, Is.EqualTo(first.name));
            Assert.That(receiverAnimator.GetParameter(receivers, Party.Self, "OGB/Second").name, Is.EqualTo(second.name));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true).Select(r => r.parameter),
                Is.EquivalentTo(new[] { "OGB/First", "OGB/Second" }));
        }
#endif
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ParameterRequestsReuseShaderReceiversAndUnionAdvancedOutputs(bool self, bool shaderFirst) {
            var receivers = bake.socketReceivers;
            var party = self ? Party.Self : Party.Others;
            var targets = Enumerable.Range(0, 9)
                .Select(i => (Renderer)GameObjects.Create("Target " + i, root).AddComponent<MeshRenderer>()).ToArray();
            void Materials() => receivers.SetMaterials(targets, self, true, "ShaderOutput");
            if (shaderFirst) Materials();
            var parameter = receiverAnimator.GetParameter(receivers, party, advancedParameters: ContactReceiver.AdvancedParameter.SenderLocalPosition);
            if (!shaderFirst) Materials();
            var again = receiverAnimator.GetParameter(receivers, party, advancedParameters: ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.name, Is.EqualTo(parameter.name));
            var requested = receiverAnimator.GetParameter(receivers, party, suffix: ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y);
            Assert.That(requested.name, Is.EqualTo(parameter.name + ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y));
            var parameterCount = fx.parameters.Length;
            receiverAnimator.GetParameter(receivers, party);
            Assert.That(fx.parameters, Has.Length.EqualTo(parameterCount));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var all = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(all, Has.Length.EqualTo(3));
            Assert.That(all.All(r => r.allowSelf == self && r.allowOthers != self && r.writeToMaterials), Is.True);
            Assert.That(all.Count(r => !string.IsNullOrEmpty(r.parameter)), Is.EqualTo(1));
            var receiver = all.Single(r => r.parameter == parameter.name);
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
            foreach (var suffix in new[] { ContactReceiver.PARAM_SENDER_LOCAL_POSITION_X, ContactReceiver.PARAM_SENDER_LOCAL_POSITION_Y, ContactReceiver.PARAM_SENDER_LOCAL_POSITION_Z,
                         ContactReceiver.PARAM_SENDER_LOSSY_SCALE_X, ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y, ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Z }) {
                Assert.That(fx.TryGetParam(parameter.name + suffix, out _), Is.EqualTo(suffix == ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y),
                    "Enabling an SDK output group must not declare its unused animator fields");
            }
            Assert.That(receiver.advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderLocalPosition
                | ContactReceiver.AdvancedParameter.SenderLossyScale));
        }
#endif
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS && VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void MergedParameterRequestsUnionOutputsWithoutChangingSourceFlagsOrAddingSelectors() {
            var receivers = bake.socketReceivers;
            var parameter = receiverAnimator.GetParameter(receivers, Party.Merged, advancedParameters: ContactReceiver.AdvancedParameter.SenderType);
            mergedParameterName = parameter.name;
            var layers = fx.GetLayers().Count();
            var again = receiverAnimator.GetParameter(receivers, Party.Merged, advancedParameters: ContactReceiver.AdvancedParameter.SenderLossyScale);
            Assert.That(again.name, Is.EqualTo(parameter.name));
            _ = receiverAnimator.GetParameter(receivers, Party.Merged, suffix: ContactReceiver.PARAM_SENDER_TYPE);
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
            foreach (var self in new[] { true, false }) {
                Assert.That(MergedReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.SenderType
                    | ContactReceiver.AdvancedParameter.SenderLossyScale));
                Assert.That(SourceReceiver(self).advancedParameters, Is.EqualTo(ContactReceiver.AdvancedParameter.PointSeparation));
            }
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameter.name + ContactReceiver.PARAM_SENDER_TYPE), Is.Not.Null);
            Assert.That(fx.parameters.FirstOrDefault(p => p.name == parameter.name + ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y), Is.Null);
        }
#endif
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
                Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty,
                    "Material requests reserve receivers without creating SDK components before finalization");
            }
            var merged = receiverAnimator.GetParameter(bake.socketReceivers, Party.Merged);
            Start(shaderFirst ? null : Shader);
            var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
            Assert.That(contacts, Has.Length.EqualTo(5)); // Quartet plus shader-only path buffer.
            Assert.That(contacts.All(c => c.transform.IsChildOf((Transform)bake.bakeRoot)), Is.True);
            Assert.That(contacts.Select(c => c.name), Is.EquivalentTo(new[] {
                "Receiver - Sockets (Self)", "Receiver - Sockets (Others)", "Receiver - Socket Paths",
                "Receiver - Sockets (Self Merged)", "Receiver - Sockets (Others Merged)"
            }));
            Assert.That(SourceReceiver(true).writeToMaterials && SourceReceiver(false).writeToMaterials, Is.True);
            Assert.That(MergedReceiver(true).writeToMaterials || MergedReceiver(false).writeToMaterials, Is.False);
            Assert.That(SourceReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.EqualTo(MergedReceiver(false).parameter));
            Assert.That(MergedReceiver(true).parameter, Is.Not.EqualTo(SourceReceiver(true).parameter).And.Not.EqualTo(SourceReceiver(false).parameter));
            Assert.That(merged.name, Is.EqualTo(MergedReceiver(true).parameter));
        }
#endif

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void ExtraMaterialReceiversHaveNoParametersOrMergedCopies() {
            var renderers = new List<Renderer> { plug.configureTpsMesh.Single() };
            for (var i = 1; i < 9; i++) renderers.Add(GameObjects.Create("Extra renderer " + i, root).AddComponent<MeshRenderer>());
            var receiverService = injector.GetService<SpsPlugReceiverService>();
            receiverService.Create(plug, renderers);
            Start();
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
    }
}
