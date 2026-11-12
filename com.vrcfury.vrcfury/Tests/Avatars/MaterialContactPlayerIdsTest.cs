using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VF.Hooks.VrcsdkFixes;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class MaterialContactPlayerIdsTest {
        [Test]
        public void ContactsShareStablePositiveIdsWithoutChangingPhysBones() {
            if (HarmonyTest.PatchingError != null) Assert.Ignore(HarmonyTest.PatchingError);
            typeof(FixMaterialContactPlayerIdsHook).VFStaticMethod("Init").Invoke(null, null);
            var root = new GameObject("Player ID test");
            try {
                var setup = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Avatars.AvatarDynamicsSetup");
                var contacts = setup.VFStaticMethod("Contact_OnInitialize");
                var physBones = setup.VFStaticMethod("PhysBone_OnInitialize");
                var colliders = setup.VFStaticMethod("PhysBoneCollider_OnPreShapeInitialize");
                var ids = new HashSet<int>();
                for (var i = 0; i < 32; i++) {
                    var avatar = new GameObject("Avatar " + i);
                    avatar.transform.SetParent(root.transform, false);
                    var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                    var child = new GameObject("Dynamics");
                    child.transform.SetParent(avatar.transform, false);
                    var sender = child.AddComponent<VRCContactSender>();
                    var receiver = child.AddComponent<VRCContactReceiver>();
                    var physBone = child.AddComponent<VRCPhysBone>();
                    var collider = child.AddComponent<VRCPhysBoneCollider>();
                    contacts.Invoke(null, new object[] { sender });
                    contacts.Invoke(null, new object[] { receiver });
                    physBones.Invoke(null, new object[] { physBone });
                    colliders.Invoke(null, new object[] { collider });
                    var id = sender.playerId;
                    Assert.That(id, Is.GreaterThan(0));
                    Assert.That(ids.Add(id), Is.True, "Each avatar must have its own material player ID");
                    Assert.That(receiver.playerId, Is.EqualTo(id));
                    Assert.That(physBone.playerId, Is.EqualTo(descriptor.GetHashCode()));
                    Assert.That(collider.playerId, Is.EqualTo(descriptor.GetHashCode()));
                    Assert.That(Math.Max(id, 0), Is.EqualTo(id), "Material packing must preserve the player ID");
                    sender.playerId = -1;
                    contacts.Invoke(null, new object[] { sender });
                    Assert.That(sender.playerId, Is.EqualTo(id), "Reinitializing must keep the avatar's ID");
                }
                var detached = root.AddComponent<VRCContactSender>();
                contacts.Invoke(null, new object[] { detached });
                Assert.That(detached.playerId, Is.EqualTo(-1));
                Assert.That(ids.Contains(Math.Max(detached.playerId, 0)), Is.False);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
