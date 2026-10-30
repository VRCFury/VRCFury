using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class HapticContactsServiceTest {
        [TestCase(HumanBodyBones.Hips, "_SelfNotOnHips")]
        [TestCase(HumanBodyBones.Head, "")]
        public void SelfSuffixDependsOnHipLocation(HumanBodyBones bone, string suffix) {
            var obj = new GameObject("Receiver");
            try {
                var closestBone = new ClosestBoneUtils(null, null);
                var cache = (Dictionary<VFGameObject, HumanBodyBones?>)typeof(ClosestBoneUtils).VFField("results").GetValue(closestBone);
                cache[obj] = bone;
                var service = new HapticContactsService();
                typeof(HapticContactsService).VFField("closestBoneUtils").SetValue(service, closestBone);
                Assert.That(service.GetSelfTagSuffix(obj), Is.EqualTo(suffix));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void ReceiverUsesCallerTagsWithoutRewritingOrMutatingThem() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Desktop contact receivers only");
            var obj = new GameObject("Receiver");
            try {
                var sources = new LayerSourceService();
                typeof(LayerSourceService).VFField("globals").SetValue(sources, new GlobalsService());
                var fx = new ControllerManager(VFController.Create(), null, VRCAvatarDescriptor.AnimLayerType.FX,
                    () => 0, () => "", name => name, sources);
                var controllers = new ControllersService();
                var cache = (Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>)
                    typeof(ControllersService).VFField("_controllers").GetValue(controllers);
                cache[VRCAvatarDescriptor.AnimLayerType.FX] = fx;
                var service = new HapticContactsService();
                typeof(HapticContactsService).VFField("controllers").SetValue(service, controllers);
                typeof(HapticContactsService).VFField("overlappingService").SetValue(service, new OverlappingContactsFixService());
                // No bone resolver is installed: AddReceiver must not make hip-policy decisions.
                var tags = new[] { "Hand", HapticUtils.TagTpsOrfRoot + "_SelfNotOnHips", "SPS/Socket/public" };
                service.AddReceiver(new HapticContactsService.ReceiverRequest {
                    obj = obj, objName = "Self", paramName = "Self",
                    tags = tags, party = HapticUtils.ReceiverParty.Self
                });
                Assert.That(obj.GetComponentsInChildren<VRCContactReceiver>().Single().collisionTags, Is.EqualTo(tags));
                Assert.That(tags, Is.EqualTo(new[] { "Hand", "TPS_Orf_Root_SelfNotOnHips", "SPS/Socket/public" }));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
