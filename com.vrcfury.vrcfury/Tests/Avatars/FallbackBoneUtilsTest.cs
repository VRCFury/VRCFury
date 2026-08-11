using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FallbackBoneUtilsTest {
        [Test]
        public void DistalFallsBackAlongItsOwnFingerFirst() {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(HumanBodyBones.LeftIndexDistal).Take(2),
                Is.EqualTo(new[] {
                    HumanBodyBones.LeftIndexIntermediate,
                    HumanBodyBones.LeftIndexProximal
                })
            );
        }

        [Test]
        public void LittleFingerTriesRingFirstAndRecursesToHips() {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(HumanBodyBones.RightLittleDistal),
                Is.EqualTo(new[] {
                    HumanBodyBones.RightLittleIntermediate,
                    HumanBodyBones.RightLittleProximal,
                    HumanBodyBones.RightRingDistal,
                    HumanBodyBones.RightRingIntermediate,
                    HumanBodyBones.RightRingProximal,
                    HumanBodyBones.RightMiddleDistal,
                    HumanBodyBones.RightMiddleIntermediate,
                    HumanBodyBones.RightMiddleProximal,
                    HumanBodyBones.RightIndexDistal,
                    HumanBodyBones.RightIndexIntermediate,
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightThumbDistal,
                    HumanBodyBones.RightThumbIntermediate,
                    HumanBodyBones.RightThumbProximal,
                    HumanBodyBones.RightHand,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightShoulder,
                    HumanBodyBones.UpperChest,
                    HumanBodyBones.Chest,
                    HumanBodyBones.Spine,
                    HumanBodyBones.Hips
                })
            );
        }

        [Test]
        public void RingFingerTriesMiddleThenLittle() {
            var fallbacks = FallbackBoneUtils.GetFallbacks(HumanBodyBones.LeftRingProximal).ToArray();
            Assert.That(fallbacks.Take(2), Is.EqualTo(new[] {
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftLittleProximal
            }));
        }

        [TestCase(HumanBodyBones.Jaw, HumanBodyBones.Head)]
        [TestCase(HumanBodyBones.LeftEye, HumanBodyBones.Head)]
        [TestCase(HumanBodyBones.UpperChest, HumanBodyBones.Chest)]
        [TestCase(HumanBodyBones.Chest, HumanBodyBones.Spine)]
        [TestCase(HumanBodyBones.Neck, HumanBodyBones.UpperChest)]
        [TestCase(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot)]
        public void OptionalBoneUsesNearbyFallback(HumanBodyBones requested, HumanBodyBones fallback) {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(requested).Take(1),
                Is.EqualTo(new[] { fallback })
            );
        }

        [Test]
        public void JawRecursesThroughTheTorsoToHips() {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(HumanBodyBones.Jaw),
                Is.EqualTo(new[] {
                    HumanBodyBones.Head,
                    HumanBodyBones.Neck,
                    HumanBodyBones.UpperChest,
                    HumanBodyBones.Chest,
                    HumanBodyBones.Spine,
                    HumanBodyBones.Hips
                })
            );
        }

        [Test]
        public void ToesRecurseThroughTheLegToHips() {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(HumanBodyBones.LeftToes),
                Is.EqualTo(new[] {
                    HumanBodyBones.LeftFoot,
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.Hips
                })
            );
        }

        [Test]
        public void HipsHasNoFallbacks() {
            Assert.That(
                FallbackBoneUtils.GetFallbacks(HumanBodyBones.Hips),
                Is.Empty
            );
        }

        [Test]
        public void EveryHumanoidBoneEventuallyFallsBackToHips() {
            foreach (var bone in Enum.GetValues(typeof(HumanBodyBones))
                         .Cast<HumanBodyBones>()
                         .Where(bone => bone != HumanBodyBones.LastBone)) {
                Assert.That(
                    new[] { bone }.Concat(FallbackBoneUtils.GetFallbacks(bone)).Last(),
                    Is.EqualTo(HumanBodyBones.Hips),
                    bone.ToString()
                );
            }
        }
    }
}
