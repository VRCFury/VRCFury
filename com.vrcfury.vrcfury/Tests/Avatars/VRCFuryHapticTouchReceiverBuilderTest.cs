using System;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class VRCFuryHapticTouchReceiverBuilderTest {
        [TestCase(false)]
        [TestCase(true)]
        public void BakesTheSameShapeForSelfAndOthers(bool capsule) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var root = new GameObject("Touch zone");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var zone = root.AddComponent<VRCFuryHapticTouchReceiver>();
                zone.name = "Test";
                if (capsule) zone.height = 0.6f;
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
                injector.GetService<VRCFuryHapticTouchReceiverBuilder>().Apply();

                var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true);
                Assert.That(receivers, Has.Length.EqualTo(2));
                foreach (var receiver in receivers) {
                    Assert.That(receiver.shapeType, Is.EqualTo(capsule
                        ? ContactBase.ShapeType.Capsule
                        : ContactBase.ShapeType.Sphere));
                    Assert.That(receiver.radius, Is.EqualTo(zone.radius));
                    if (capsule) {
                        Assert.That(receiver.height, Is.EqualTo(zone.height));
                        Assert.That(receiver.rotation, Is.EqualTo(Quaternion.identity));
                    }
                }
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
