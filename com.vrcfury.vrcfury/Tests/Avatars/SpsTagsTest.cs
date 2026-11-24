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

    internal class SpsTagsTest : SpsSenderBakeTestBase {
        [Test]
        public void FormatsNativeSenderTags() {
            Assert.That(SpsTags.FormatPlug("nothips"), Is.EqualTo("SPS/Plug/nothips"));
            Assert.That(SpsTags.FormatSocket("_public"), Is.EqualTo("SPS/Socket/_public"));
            Assert.That(SpsTags.FormatSocketPath("custom"), Is.EqualTo("SPS/SocketPath/custom"));
        }

        [Test]
        public void ScaleProtocolRoundTripsEveryGroupAndPathIndex() {
            foreach (var scale in new[] { 0.001f, 0.1f, 1, 100f }) {
                for (var group = 1; group <= HapticUtils.GroupCount; group++) {
                    for (var pathIndex = 0; pathIndex < 8; pathIndex++) {
                        for (var flagBits = 0; flagBits < 32; flagBits++) {
                            var encoded = HapticUtils.EncodeSocketScale(group, pathIndex, (ScaleFlags)(flagBits << 10)) * scale;
                            var packed = Mathf.RoundToInt(encoded.x / encoded.z);
                            Assert.That(packed & 127, Is.EqualTo(group));
                            Assert.That((packed >> 7) & 7, Is.EqualTo(pathIndex));
                            Assert.That((packed >> 10) & 31, Is.EqualTo(flagBits));
                            Assert.That(encoded.y / encoded.z, Is.EqualTo(1f).Within(0.000001));
                        }
                    }
                }
            }
        }

        [Test]
        public void ScaleProtocolKeepsGroupAndIndexSeparateFromCollisionFlags() {
            Assert.That((int)ScaleFlags.UnlockAll, Is.EqualTo(1 << 10));
            Assert.That((int)ScaleFlags.UnlockLocalX, Is.EqualTo(1 << 11));
            Assert.That((int)ScaleFlags.RadiusOffset, Is.EqualTo(1 << 12));
            Assert.That((int)ScaleFlags.Twoway, Is.EqualTo(1 << 13));
            Assert.That((int)ScaleFlags.Collapse, Is.EqualTo(1 << 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => HapticUtils.EncodeSocketScale(1, 0, (ScaleFlags)1));
            Assert.That(HapticUtils.EncodeSocketScale(1, 0), Is.EqualTo(new Vector3(1, 1f, 1)));
            Assert.That(HapticUtils.EncodeSocketScale(37, 2), Is.EqualTo(new Vector3(293, 1f, 1)));
            Assert.That(HapticUtils.EncodeSocketScale(127, 7), Is.EqualTo(new Vector3(1023, 1f, 1)));
            var allFlags = ScaleFlags.Collapse | ScaleFlags.Twoway | ScaleFlags.RadiusOffset | ScaleFlags.UnlockLocalX | ScaleFlags.UnlockAll;
            Assert.That(HapticUtils.EncodeSocketScale(127, 7, allFlags), Is.EqualTo(new Vector3(32767, 1f, 1)));
            var sender = GameObjects.Create("Encoded", root);
            sender.worldScale = HapticUtils.EncodeSocketScale(127, 7, allFlags);
            root.transform.localScale *= 3;
            var matrix = sender.localToWorldMatrix;
            CheckVector(matrix.GetColumn(0).normalized, root.transform.right);
            CheckVector(matrix.GetColumn(1).normalized, root.transform.up);
            CheckVector(matrix.GetColumn(2).normalized, root.transform.forward);
            var normalization = matrix.GetColumn(2).magnitude;
            Assert.That(Mathf.RoundToInt(matrix.GetColumn(0).magnitude / normalization), Is.EqualTo(32767));
            Assert.That(matrix.GetColumn(1).magnitude / normalization, Is.EqualTo(1f).Within(0.000001));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(128, 0)]
        [TestCase(1, -1)]
        [TestCase(1, 8)]
        public void RejectsUnrepresentableProtocolValues(int group, int pathIndex) {
            Assert.Throws<ArgumentOutOfRangeException>(() => HapticUtils.EncodeSocketScale(group, pathIndex));
        }

        [TestCase(0f, 1f)]
        [TestCase(float.Epsilon, 2f)]
        [TestCase(.000001f, 2.000001f)]
        [TestCase(.00001f, 2.00001f)]
        [TestCase(.05f, 2.05f)]
        [TestCase(.5f, 2.5f)]
        [TestCase(1f, 3f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void SocketScaleEncodesRadiusCapWithoutChangingOtherMetadata(float cap, float encodedY) {
            var flags = ScaleFlags.RadiusOffset | ScaleFlags.Collapse;
            var encoded = HapticUtils.EncodeSocketScale(37, 2, flags, cap);
            Assert.That(encoded.x, Is.EqualTo(293 | (int)flags));
            Assert.That(encoded.y, Is.EqualTo(encodedY));
            Assert.That(encoded.z, Is.EqualTo(1));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void SocketScaleRejectsInvalidRadiusCaps(float cap) {
            Assert.Throws<ArgumentOutOfRangeException>(() => HapticUtils.EncodeSocketScale(1, 0, maxRadiusOffset: cap));
        }

        [TestCase(0f, 0.03f)]
        [TestCase(float.NaN, 0.03f)]
        [TestCase(float.PositiveInfinity, 0.03f)]
        [TestCase(0.4f, 0f)]
        [TestCase(0.4f, float.NaN)]
        [TestCase(0.4f, float.PositiveInfinity)]
        public void EncodedPlugRequiresFinitePositiveDimensions(float length, float radius) {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HapticUtils.EncodePlugScale(length, radius));
        }

        [TestCase(0.03f, 0.03f)]
        [TestCase(0.4f, 0.4f)]
        [TestCase(0.5f, 0.4f)]
        public void EncodedPlugClampsRadiusToLength(float radius, float expectedRadius) {
            Assert.That(HapticUtils.EncodePlugScale(0.4f, radius),
                Is.EqualTo(new Vector3(expectedRadius, expectedRadius, 0.4f)));
        }
    }
}
