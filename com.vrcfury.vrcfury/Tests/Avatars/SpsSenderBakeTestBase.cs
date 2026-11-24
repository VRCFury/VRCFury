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

namespace VF.Tests {
    internal abstract class SpsSenderBakeTestBase {
        protected GameObject root;
        protected VRCFuryInjector injector;

        protected string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;

        [SetUp]
        public void SetUp() {
            root = new GameObject("SPS3 Test");
            root.transform.position = new Vector3(1, 2, 3);
            root.transform.rotation = Quaternion.Euler(31, 47, 13);
            root.transform.localScale = Vector3.one * 2;
            injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        protected SpsUpgrader CreateUpgrader() {
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            return VRCFuryInjectorBuilder.CreateForManualEditing(root).GetService<SpsUpgrader>();
        }

        protected static VRCContactSender[] NativeSocketSenders(VFGameObject parent) =>
            parent.GetComponentsInSelfAndChildren<VRCContactSender>()
                .Where(c => c.collisionTags.Any(t => t == SpsTags.FormatSocket("_public")
                    || t.StartsWith("SPS/Socket/") || t.StartsWith("SPS/SocketPath/")))
                .ToArray();

        protected class AutoChannels : SpsAutoChannelGenerator {
            public HumanBodyBones bone;
            public AutoChannels(HumanBodyBones bone) => this.bone = bone;
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => bone;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

        protected static float MaxScale(Transform transform) {
            var scale = transform.lossyScale;
            return Mathf.Max(scale.x, scale.y, scale.z);
        }

        protected static void CheckVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Mathf.Max(0.0001f, expected.magnitude * 0.000001f)));

        protected static void CheckNode(
            VRCContactSender sender,
            int group,
            int pathIndex,
            Flags flags,
            float inheritedScale = 1,
            bool radiusOffset = false,
            bool animatorHole = false
        ) {
            var scale = sender.transform.lossyScale / inheritedScale;
            var packed = Mathf.RoundToInt(scale.x);
            Assert.That(packed & 127, Is.EqualTo(group));
            Assert.That((packed >> 7) & 7, Is.EqualTo(pathIndex));
            Assert.That(scale.y, Is.EqualTo(1f).Within(0.000001));
            Assert.That(Mathf.RoundToInt(scale.z), Is.EqualTo(animatorHole ? (int)SpsTags.SocketScaleFlagsZ.AnimatorHole | 1 : 1));
            Assert.That(sender.collisionTags.Any(tag => tag.StartsWith("SPS/Flag/")), Is.False);
            var expectedBits = flags == Flags.Hole ? 1 << 14 : flags == Flags.Ring ? 1 << 13 : 0;
            Assert.That(packed & 0x7C00, Is.EqualTo(expectedBits | (radiusOffset ? 1 << 12 : 0)));
            Assert.That(NativeSocketSenders(sender.owner()), Does.Contain(sender));
            Assert.That(sender.collisionTags.Count, Is.LessThanOrEqualTo(16));
            Assert.That(sender.radius, Is.Zero);
        }
    }
}
