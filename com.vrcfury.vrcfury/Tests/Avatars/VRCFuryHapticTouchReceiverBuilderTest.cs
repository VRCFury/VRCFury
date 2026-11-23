using System;
using System.Linq;
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
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class VRCFuryHapticTouchReceiverBuilderTest {
        [TestCase(false)]
        [TestCase(true)]
        public void ExcludesTouchZoneFromPhysbonesAndOnlyBakesContactsOnDesktop(bool capsule) {
            var root = new GameObject("Touch zone");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var physbone = root.AddComponent<VRCPhysBone>();
                var zoneObject = new GameObject("Zone");
                zoneObject.transform.SetParent(root.transform, false);
                var zone = zoneObject.AddComponent<VRCFuryHapticTouchReceiver>();
                zone.name = "Test";
                if (capsule) zone.height = 0.6f;
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                var initialParams = fx.parameters.Select(p => p.name).ToArray();
                injector.GetService<VRCFuryHapticTouchReceiverBuilder>().Apply();

                Assert.That(physbone.ignoreTransforms, Does.Contain(zoneObject.transform));
                var receivers = root.GetComponentsInChildren<VRCContactReceiver>(true);
                if (!BuildTargetUtils.IsDesktop()) {
                    Assert.That(receivers, Is.Empty);
                    Assert.That(root.GetComponentsInChildren<VRCContactSender>(true), Is.Empty);
                    Assert.That(zoneObject.transform.childCount, Is.Zero);
                    Assert.That(fx.parameters.Select(p => p.name), Is.EquivalentTo(initialParams));
                    return;
                }
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
