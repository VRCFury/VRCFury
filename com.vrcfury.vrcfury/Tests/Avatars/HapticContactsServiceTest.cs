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
    [Category("VRCFury")]
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
