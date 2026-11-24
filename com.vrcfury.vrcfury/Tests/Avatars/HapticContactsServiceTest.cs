using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    public class HapticContactsServiceTest {
        [Test]
        public void OgbIdsNormalizeSlashesBeforeCheckingUniquenessWithoutChangingMenuNames() {
            var ids = new HashSet<string>();
            Assert.That(HapticUtils.MakeUniqueOgbId(ids, "Front/Socket"), Is.EqualTo("Front_Socket"));
            Assert.That(HapticUtils.MakeUniqueOgbId(ids, "Front_Socket"), Is.EqualTo("Front_Socket_2"));
            Assert.That(HapticUtils.MakeUniqueOgbId(ids, "Front/Socket"), Is.EqualTo("Front_Socket_3"));
            Assert.That(HapticUtils.MakeUniqueId(new HashSet<string>(), "Front/Socket"), Is.EqualTo("Front/Socket"));
        }

        [Test]
        public void ReceiverUsesCallerTagsWithoutRewritingOrMutatingThem() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Desktop contact receivers only");
            var obj = new GameObject("Receiver");
            try {
                var avatar = obj.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
                var service = injector.GetService<HapticContactsService>();
                var tags = new[] { "Hand", SpsTags.PlugClose, "SPS/Socket/public" };
                service.AddReceiver(new HapticContactsService.ReceiverRequest {
                    obj = obj, objName = "Self", paramName = "Self",
                    tags = tags, party = HapticUtils.ReceiverParty.Self
                });
                Assert.That(obj.GetComponentsInChildren<VRCContactReceiver>().Single().collisionTags, Is.EqualTo(tags));
                Assert.That(tags, Is.EqualTo(new[] { "Hand", SpsTags.PlugClose, "SPS/Socket/public" }));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
    }
}

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

    internal class HapticContactsServiceTagRoutingTest : SpsAvatarBuildTestBase {
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
                var rawScale = scaleDetector.GetRawWorldScale(plugRoot, "Another consumer");
                Assert.That(rawScale.name, Is.EqualTo(lengthReceiver.parameter));
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
                playable.SetFloat(lengthReceiver.parameter, 0.02f);
                Evaluate();
                Assert.That(playable.GetFloat(lengthReceiver.parameter) * lengthMult.defaultValue, Is.EqualTo(0.8f).Within(0.001f));
                Set(socketSelf, 0, -1);
                Set(plugSelf, 0, -1);
                Evaluate();
                Assert.That(playable.GetFloat(socketSelf.parameter), Is.Zero);
                Assert.That(playable.GetFloat(plugSelf.parameter), Is.Zero);
            } finally {
                UnityEditor.EditorPrefs.SetBool("com.vrcfury.haptics", previousHaptics);
            }
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

    internal class HapticContactsServiceShapeUnitsTest : SpsSenderBakeTestBase {
        [TestCase(true)]
        [TestCase(false)]
        public void SenderFactoryAcceptsWorldScaleWithWorldOrLocalShapeUnits(bool useWorldUnits) {
            var sender = HapticSenderFactory.AddSender(new HapticSenderFactory.SenderRequest {
                obj = root,
                objName = "Scaled Capsule",
                pos = new Vector3(0.02f, 0.04f, 0.08f),
                radius = 0.03f,
                height = 0.3f,
                rotation = Quaternion.Euler(90, 0, 0),
                worldScale = new Vector3(0.2f, 0.4f, 0.8f),
                useWorldUnits = useWorldUnits,
                tags = new[] { "Finger", SpsTags.FormatPlug("_public") }
            });
            if (!BuildTargetUtils.IsDesktop()) {
                Assert.That(sender, Is.Null);
                return;
            }
            var contact = sender.GetComponent<VRCContactSender>();
            Assert.That(contact.collisionTags, Is.EquivalentTo(new[] { "Finger", SpsTags.FormatPlug("_public") }));
            CheckVector(sender.worldScale, new Vector3(0.2f, 0.4f, 0.8f));
            CheckVector(Vector3.Scale(contact.position, sender.worldScale),
                useWorldUnits ? new Vector3(0.02f, 0.04f, 0.08f) : new Vector3(0.004f, 0.016f, 0.064f));
            Assert.That(contact.radius * MaxScale(sender), Is.EqualTo(useWorldUnits ? 0.03f : 0.024f).Within(0.000001));
            Assert.That(contact.height * MaxScale(sender), Is.EqualTo(useWorldUnits ? 0.3f : 0.24f).Within(0.000001));
        }
    }
}
