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
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

namespace VF.Tests {
    internal class SelfAndOthersReceiverAnimatorServiceTest {
        [Test]
        public void SelectsTheClosestContactAndDisablesBothReceiversAtIdle() {
            var root = new GameObject("Avatar");
            var assets = new List<Object>();
            var graph = default(PlayableGraph);
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
                var tags = new[] { "Test" };
                var channels = new SpsChannelContacts(avatar, _ => new SpsChannelsService.ChannelSet(false, tags, tags, tags));
                var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(new SelfAndOthersReceiverService.Request {
                    frame = root,
                    name = "Test contact",
                    worldRadius = 1,
                    channels = channels,
                    getTags = (_, _) => tags
                });
                var receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                receiverAnimator.GetParameter(receivers, Party.Merged);
                var self = receiverAnimator.GetAdvancedParam(receivers, Party.Self, "PointSeparation");
                var others = receiverAnimator.GetAdvancedParam(receivers, Party.Others, "PointSeparation");
                Assert.That(self.GetDefault(), Is.EqualTo(-1));
                Assert.That(others.GetDefault(), Is.EqualTo(-1));
                var selector = new AnimatorIterator.Trees().From(fx).Single(tree => tree.BlendParameter == others.name);
                Assert.That(selector.children.Count, Is.EqualTo(5));
                var mergedSelf = receivers.Find(true, true).obj;
                var mergedOthers = receivers.Find(false, true).obj;
                injector.GetService<SpsChannelsService>().Apply(channels, _ => { });
                Assert.That(mergedSelf.active || mergedOthers.active, Is.False);
                injector.GetService<IsObjectEnabledService>().Apply();
                injector.GetService<ObjectTreeService>().Apply();
                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(param => param.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Merged receiver selector");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float selfValue, float othersValue, bool expectSelf, bool expectOthers, int frames = 1) {
                    playable.SetFloat(self, selfValue);
                    playable.SetFloat(others, othersValue);
                    for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
                    Assert.That(mergedSelf.active, Is.EqualTo(expectSelf));
                    Assert.That(mergedOthers.active, Is.EqualTo(expectOthers));
                }

                graph.Evaluate(1f / 60);
                Assert.That(mergedSelf.active || mergedOthers.active, Is.False);
                Check(0, -1, true, false);
                Check(-1, 0, false, true);
                Check(0, .5f, true, false);
                Check(.5f, 0, false, true);
                Check(.5f, .5f, false, true);
                Check(0, 0, false, true);
                Check(-1, -1, false, false);
#else
                Assert.That(() => receiverAnimator.GetParameter(receivers, Party.Merged),
                    Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired));
                Assert.That(receivers.IsCreated, Is.False);
#endif
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
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

    internal class SelfAndOthersReceiverAdvancedParametersTest : SpsAvatarBuildTestBase {
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
                var expected = party != Party.Self && othersDistance > 0
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
            Set(receiver, 1, 0, native: false);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero, "Zero separation must not count as a contact");
            Set(receiver, 0, 0.2f, native: false);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(shared && party != Party.Self ? 1 : 0),
                "Positive separation counts as a contact even at zero proximity");
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
    }
}
#endif

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

    internal class SelfAndOthersReceiverPartySelectionTest : SpsPlugRadiusTestBase {
        [Test]
        public void SelectsOneMergedReceiverWithoutBufferingItsParameter() {
            Start();
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(4));
            Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.False);
            Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.False);
            foreach (var values in new[] { (0.9f, 0.1f, true), (0.1f, 0.9f, false), (0.8f, 0.2f, true), (0f, 0f, false) }) {
                var hasContact = values.Item1 > 0 || values.Item2 > 0;
                playable.SetFloat(SourceReceiver(true).parameter, values.Item1);
                playable.SetFloat(SourceReceiver(false).parameter, values.Item2);
                playable.SetFloat(SourceReceiver(true).parameter + "_PointSeparation", values.Item1 == 0 ? -1 : 1 - values.Item1);
                playable.SetFloat(SourceReceiver(false).parameter + "_PointSeparation", values.Item2 == 0 ? -1 : 1 - values.Item2);
                // SDK output deliberately differs from both selector inputs. Sampling must use
                // this current merged value immediately, rather than max/self/other from a DBT.
                SetMergedProximity(hasContact ? 0.6f : 0);
                graph.Evaluate(1f / 60);
                Assert.That(SourceReceiver(true).gameObject.activeSelf && SourceReceiver(false).gameObject.activeSelf, Is.True);
                Assert.That(MergedReceiver(true).enabled && MergedReceiver(false).enabled, Is.True);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item3));
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(hasContact && !values.Item3));
                Assert.That(playable.IsParameterControlledByCurve(MergedReceiver(true).parameter), Is.False);
                for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
                var expected = hasContact ? 0.0216f + MinimumRadius * 0.64f : MinimumRadius;
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(expected).Within(0.00002f));
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
                var hasContact = values.Item3 >= 0 || values.Item4 >= 0;
                playable.SetFloat(self, values.Item1);
                playable.SetFloat(others, values.Item2);
                playable.SetFloat(self + "_PointSeparation", values.Item3);
                playable.SetFloat(others + "_PointSeparation", values.Item4);
                SetMergedProximity(hasContact ? 0.6f : 0);
                graph.Evaluate(1f / 60);
                Assert.That(MergedReceiver(true).gameObject.activeSelf, Is.EqualTo(values.Item5), values.ToString());
                Assert.That(MergedReceiver(false).gameObject.activeSelf, Is.EqualTo(hasContact && !values.Item5), values.ToString());
                for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
                var expected = hasContact ? 0.0216f + MinimumRadius * 0.64f : MinimumRadius;
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(expected).Within(0.00002f));
            }
        }
#endif

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
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
                var self = Sender(SelfTag("SPS/Socket/first"), 1);
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

    internal class SelfAndOthersReceiverChannelSetsTest : SpsChannelBuildTestBase {
        [Test]
        public void SingleSetDoesNotControlMergedReceiverActivation() {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            Assert.Ignore(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var bake = BakePlug(Plug(obj, true));
            injector.GetService<SelfAndOthersReceiverAnimatorService>().GetParameter(bake.socketReceivers, Party.Merged);
            var self = bake.socketReceivers.Find(true, true).obj;
            var others = bake.socketReceivers.Find(false, true).obj;
            others.active = true;
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(self.active, Is.False);
            Assert.That(others.active, Is.True);
            Assert.That(bake.channelContacts.Default.clip.GetFloatBindings()
                .Any(binding => binding.propertyName == "m_IsActive"), Is.False,
                "A single channel set must not add activation curves that compete with the merged selector");
        }

        [TestCase(false, (int)Party.Self, true)]
        [TestCase(true, (int)Party.Self, true)]
        [TestCase(false, (int)Party.Self, false)]
        [TestCase(true, (int)Party.Self, false)]
        [TestCase(false, (int)Party.Others, false)]
        [TestCase(true, (int)Party.Others, false)]
        [TestCase(false, (int)Party.Merged, false)]
        [TestCase(true, (int)Party.Merged, false)]
        public void NativeOnlyClassificationDoesNotAllocateReceiversOrParameters(bool socket, int party, bool legacyEnabled) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Native only", root);
            SelfAndOthersReceiverService.Receivers main;
            if (socket) {
                var component = obj.AddComponent<VRCFuryHapticSocket>();
                main = injector.GetService<VRCFuryHapticSocketBaker>()
                    .Bake(component, legacyPlugCompatibility: legacyEnabled, avatarRoot: root).plugReceivers;
            } else {
                main = injector.GetService<VRCFuryHapticPlugBaker>()
                    .Bake(Plug(obj, true), legacySocketCompatibility: legacyEnabled).socketReceivers;
            }
            var fx = injector.GetService<ControllersService>().GetFx();
            var count = fx.parameters.Length;
            var condition = injector.GetService<SelfAndOthersReceiverAnimatorService>().GetNative(main, (Party)party);
            var native = VFClip.Create("Native");
            Assert.That(condition.create(native, VFClip.Create("Legacy")), Is.SameAs(native));
            Assert.That(condition.defaultIsTrue, Is.True);
            Assert.That(main.IsCreated, Is.False);
            Assert.That(fx.parameters, Has.Length.EqualTo(count));
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [Test]
        public void FinalizedReceiversRejectRepeatedRequestsToo() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var bake = BakePlug(Plug(obj, true));
            var animator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
            animator.GetAdvancedParam(bake.socketReceivers, Party.Self, "PointSeparation");
            animator.GetParameter(bake.socketReceivers, Party.Merged);
            animator.GetNative(bake.socketReceivers, Party.Others);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            Assert.That(() => animator.GetAdvancedParam(bake.socketReceivers, Party.Self, "PointSeparation"),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetParameter(bake.socketReceivers, Party.Self),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetParameter(bake.socketReceivers, Party.Merged),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetNative(bake.socketReceivers, Party.Others),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetNative(bake.socketReceivers, Party.Self),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetAdvancedParam(bake.socketReceivers, Party.Self, "SenderVelocityZ"),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => animator.GetParameter(bake.socketReceivers, Party.Others),
                Throws.Exception.With.Message.Contains("finalized"));
            Assert.That(() => bake.socketReceivers.SetMaterials(new[] { obj.AddComponent<MeshRenderer>() }, true, false, "SPS_Self"),
                Throws.Exception.With.Message.Contains("finalized"));
        }
#endif
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [TestCase(true)]
        [TestCase(false)]
        public void OscAdvancedOutputsDoNotCreateUnusedAnimatorParameters(bool self) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject obj = GameObjects.Create("Plug", root);
            var bake = BakePlug(Plug(obj, true));
            injector.GetService<SelfAndOthersReceiverAnimatorService>().RegisterOgbDepth(bake.socketReceivers, "OGB/Plug/Test");
            var raw = "OGB/Plug/Test/Depth" + (self ? "Self" : "Others");
            Track(injector.GetService<ChangeSpsChannelsActionBuilder>().Build(Action(obj, false), false));
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var fx = injector.GetService<ControllersService>().GetFx();
            var contacts = obj.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => r.parameter == raw).ToArray();
            Assert.That(contacts, Has.Length.EqualTo(2));
            Assert.That(contacts.All(r => r.localOnly), Is.True);
            Assert.That(contacts.All(r => r.advancedParameters == VRC.Dynamics.ContactReceiver.AdvancedParameter.All), Is.True);
            var outputs = contacts.SelectMany(receiver => receiver.GetParameterNames()).Where(name => name != raw).Distinct().ToArray();
            foreach (var tag in new[] { "old", "new" }) {
                Assert.That(outputs, Does.Contain(raw + "_CollisionTag_" + SpsTags.FormatSocket(self ? SelfTag(tag) : tag)));
            }
            foreach (var name in outputs) {
                Assert.That(fx.TryGetParam(name, out _), Is.False, "OSC-only outputs must not create unused animator parameters: " + name);
                Assert.That(new AnimatorIterator.Clips().From(fx).SelectMany(c => c.GetAllBindings()).Any(b => b.propertyName == name), Is.False);
            }
        }
#endif
    }
}
