#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using static VF.Inspector.VRCFuryHapticPlugEditor;

namespace VF.Tests {
    internal abstract class AdvancedContactParameterBridgeTestBase : SpsAvatarBuildTestBase {
        protected void VerifyAdvancedContactParameterBridge(
            Action<VRCContactReceiver, Func<string, IAnimParameterAccess>> bind
        ) {
            measurements = Socket()(Party.Self);
            var length = measurements(SpsPlugLengthMeters);
            var radius = measurements(SpsPlugRadiusMeters);
            var depth = measurements(SpsDepthMeters);
            var velocity = measurements(SpsVelocityMeters);
            Start();
            var receiver = Main();
            var managerObject = new GameObject("Advanced SDK bridge test");
            var previousManager = ContactManager.Inst;
            ContactManager manager = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo Field(Type type, string name) {
                for (; type != null; type = type.BaseType) {
                    var field = type.GetField(name, flags);
                    if (field != null) return field;
                }
                throw new Exception("Missing SDK field " + name);
            }
            object Call(object target, string name, params object[] args) {
                for (var type = target.GetType(); type != null; type = type.BaseType) {
                    var method = type.GetMethods(flags | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length);
                    if (method != null) return method.Invoke(target, args);
                }
                throw new Exception("Missing SDK method " + name);
            }
            try {
                manager = managerObject.AddComponent<ContactManager>();
                Field(typeof(ContactManager), "Inst").SetValue(null, manager);
                if (!(bool)Field(typeof(ContactManager), "hasInit").GetValue(manager)) Call(manager, "Init");
                receiver.playerId = 1;
                Field(receiver.GetType(), "_assignedUsage").SetValue(receiver, DynamicsUsage.Avatar);
                receiver.allowInit = true;
                Call(receiver, "Start");
                Func<string, IAnimParameterAccess> playableAccess = name => new TestParameterAccess(
                    () => playable.GetFloat(name), value => playable.SetFloat(name, value));
                receiver.paramAccess = playableAccess(receiver.parameter);
                var ordinaryAnimator = new Dictionary<string, float>();
                // Reproduce Gesture Manager replacing the main bridge while advanced SDK
                // accessors still write to a different Animator parameter store.
                receiver.RebindAdvancedParameters(name => new TestParameterAccess(
                    () => ordinaryAnimator.TryGetValue(name, out var value) ? value : 0,
                    value => ordinaryAnimator[name] = value));
                var senderObject = GameObjects.Create("Native plug", root);
                senderObject.localPosition = Vector3.forward * 0.5f;
                senderObject.localScale = new Vector3(0.04f, 0.04f, 0.4f);
                var sender = senderObject.AddComponent<VRCContactSender>();
                sender.radius = 1;
                sender.playerId = 1;
                sender.collisionTags = new List<string> { SpsTags.FormatPlug(SelfTag("test")) };
                Field(sender.GetType(), "_assignedUsage").SetValue(sender, DynamicsUsage.Avatar);
                sender.allowInit = true;
                Call(sender, "Start");
                void Tick() {
                    var schedule = typeof(ContactManager).GetMethod("ScheduleUpdateReceiversJob", flags);
                    var job = schedule.Invoke(manager, new[] { Activator.CreateInstance(schedule.GetParameters()[0].ParameterType) });
                    job.GetType().GetMethod("Complete").Invoke(job, null);
                    Call(Field(typeof(ContactManager), "collision").GetValue(manager), "Complete");
                    Call(manager, "HandleDynamicsFrameComplete");
                }
                Tick(); Tick();
                Evaluate();
                Assert.That(ordinaryAnimator[receiver.parameter + "_SenderLossyScaleZ"], Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(ordinaryAnimator[receiver.parameter + "_PointSeparation"], Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(playable.GetFloat(receiver.parameter + "_SenderLossyScaleZ"), Is.Zero);
                Assert.That(playable.GetFloat(length), Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(playable.GetFloat(radius), Is.EqualTo(0.03f).Within(0.001f));

                bind(receiver, playableAccess);
                Tick(); Tick();
                receiver.param_SenderVelocityZ.floatVal = -0.6f;
                Evaluate();
                Assert.That(playable.GetFloat(length), Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(playable.GetFloat(radius), Is.EqualTo(0.04f).Within(0.001f));
                Assert.That(playable.GetFloat(depth), Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(playable.GetFloat(velocity), Is.EqualTo(-0.6f).Within(0.001f));
                foreach (var distance in new[] { 0.2f, 0.05f }) {
                    senderObject.localPosition = Vector3.forward * distance;
                    Tick(); Tick();
                    Evaluate();
                    Assert.That(playable.GetFloat(receiver.parameter), Is.EqualTo(1).Within(0.001f));
                    Assert.That(playable.GetFloat(depth), Is.EqualTo(distance - 0.4f).Within(0.001f));
                }
            } finally {
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObject);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
        }


        // Gesture Manager's bridge constructor takes its module and parameter name.
    }
}
#endif
