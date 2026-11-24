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

    internal class WorldScaleDetectorServiceTest : SpsAvatarBuildTestBase {
        [TestCase(0.25f, true)]
        [TestCase(1f, true)]
        [TestCase(3f, true)]
        [TestCase(0.25f, false)]
        [TestCase(3f, false)]
        public void ScaleDetectorSdkContactUsesFixedCalibrationWithOptionalOgbName(float uploadScale, bool reportLength) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            root.transform.localScale = Vector3.one * uploadScale;
            var detector = injector.GetService<WorldScaleDetectorService>();
            const float bakedLength = 0.4f;
            var rawParameter = detector.GetRawWorldScale(root, "Scale consumer", reportLength ? "OGB/Plug/Test/LengthMult1" : null);
            var inverse = detector.GetInverseWorldScale(root, "Inverse consumer");
            Assert.That(detector.GetInverseWorldScale(root, "Another inverse consumer"), Is.SameAs(inverse));
            var scaleInput = detector.GetRawWorldScaleWithMultiplier(root, "Cached consumer");
            Assert.That(scaleInput.raw, Is.SameAs(rawParameter));
            var receiver = root.GetComponentsInChildren<VRCContactReceiver>(true).Single();
            Assert.That(receiver.parameter, Is.EqualTo(rawParameter.name));
            var sender = root.GetComponentsInChildren<VRCContactSender>(true).Single();
            var outer = receiver.owner().parent;
            var anchor = outer.parent;
            Assert.That(receiver.transform.localPosition.x, Is.EqualTo(1 / (100 * uploadScale)).Within(0.000001f));
            Assert.That(outer.GetComponent<ScaleConstraint>().GetSource(0).sourceTransform, Is.EqualTo(root.transform));
            if (reportLength) {
                Assert.That(receiver.parameter, Is.EqualTo("OGB/Plug/Test/LengthMult1"));
            }
            injector.GetService<IsObjectEnabledService>().Get(root).SetDefault(true);
            Start();
            var managerObject = new GameObject("Scale contact manager");
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
                outer.active = true;
                foreach (var contact in new ContactBase[] { sender, receiver }) {
                    contact.playerId = 1;
                    Field(contact.GetType(), "_assignedUsage").SetValue(contact, DynamicsUsage.Avatar);
                    contact.allowInit = true;
                    Call(contact, "Start");
                }
                var raw = 0f;
                receiver.paramAccess = new TestParameterAccess(() => raw, value => {
                    raw = value;
                    playable.SetFloat(receiver.parameter, value);
                });
                foreach (var resize in new[] { 1f, 0.25f, 2f, 10f }) {
                    root.transform.localScale = Vector3.one * (uploadScale * resize);
                    // Synchronous EditMode has no constraint stage. Apply the scale constraints'
                    // target scales, then let the SDK's actual shape and collision jobs run.
                    anchor.worldScale = Vector3.one;
                    // Exercise a distant anchor while keeping floating-point error deterministic.
                    anchor.worldPosition = new Vector3(40, 20, 0);
                    outer.worldScale = root.transform.lossyScale;
                    receiver.owner().worldScale = Vector3.one;
                    for (var i = 0; i < 3; i++) {
                        var schedule = typeof(ContactManager).GetMethod("ScheduleUpdateReceiversJob", flags);
                        var job = schedule.Invoke(manager, new[] { Activator.CreateInstance(schedule.GetParameters()[0].ParameterType) });
                        job.GetType().GetMethod("Complete").Invoke(job, null);
                        Call(Field(typeof(ContactManager), "collision").GetValue(manager), "Complete");
                        Call(manager, "HandleDynamicsFrameComplete");
                    }
                    Assert.That(raw * 100, Is.EqualTo(resize).Within(0.005f), "SDK contact output at resize " + resize);
                    if (reportLength) Assert.That(raw * (bakedLength * 100),
                        Is.EqualTo(bakedLength * resize).Within(0.002f), "OGB world length at resize " + resize);
                    Evaluate();
                    Assert.That(raw * scaleInput.multiplier, Is.EqualTo(uploadScale * resize).Within(0.005f * uploadScale));
                    Assert.That(playable.GetFloat(inverse), Is.EqualTo(1 / (uploadScale * resize)).Within(0.005f / (uploadScale * resize)));
                }
            } finally {
                Object.DestroyImmediate(root);
                if (manager != null) Call(manager, "Dispose");
                Object.DestroyImmediate(managerObject);
                Field(typeof(ContactManager), "Inst").SetValue(null, previousManager);
            }
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

    internal class WorldScaleDetectorServiceScaledLightTest : SpsPlugRadiusTestBase {
        [Test]
        public void ScaledLightUsesRawScaleWithoutDelayOrClamping(
            [Values(0.5f, 2f)] float localScale
        ) {
            var reference = GameObjects.Create("Scale reference", root);
            reference.localScale = Vector3.one * localScale;
            var light = GameObjects.Create("Scaled light", root).AddComponent<Light>();
            light.intensity = 3;
            var detector = injector.GetService<WorldScaleDetectorService>();
            var raw = detector.GetRawWorldScale(reference, "Scaled light");
            var multiplier = WorldScaleDetectorService.MaxDetectedScale * reference.worldScale.x;
            Start(() => detector.AddScaledProp(
                reference,
                new[] { ((UnityEngine.Component)light, "m_Intensity", 1.5f) }
            ));
            for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
            foreach (var weight in new[] { 0f, 0.005f, 0.01f, 1f, 2f }) {
                playable.SetFloat(raw.name, weight);
                graph.Evaluate(1f / 60);
                Assert.That(light.intensity, Is.EqualTo(1.5f * multiplier * weight).Within(0.0001f));
            }
            var cache = (Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector);
            Assert.That(cache[reference].inverse.IsValueCreated, Is.False);
        }
    }
}
