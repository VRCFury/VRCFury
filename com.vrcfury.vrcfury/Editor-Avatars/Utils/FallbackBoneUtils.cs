using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Utils {
    internal static class FallbackBoneUtils {
        public static IEnumerable<HumanBodyBones> GetFallbacks(HumanBodyBones requested) {
            if (TryGetFinger(requested, out var right, out var finger, out var segment)) {
                var first = right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
                HumanBodyBones GetFingerBone(int fingerIndex, int segmentIndex) =>
                    (HumanBodyBones)((int)first + fingerIndex * 3 + segmentIndex);

                // Prefer less-specific joints on the requested finger first.
                for (var segmentIndex = segment - 1; segmentIndex >= 0; segmentIndex--) {
                    yield return GetFingerBone(finger, segmentIndex);
                }

                // Then try nearby fingers, retaining as much specificity as possible.
                var nearbyFingers = finger switch {
                    0 => new[] { 1, 2, 3, 4 }, // Thumb -> index
                    1 => new[] { 2, 3, 4, 0 }, // Index -> middle
                    2 => new[] { 1, 3, 4, 0 }, // Middle -> index, then ring
                    3 => new[] { 2, 4, 1, 0 }, // Ring -> middle, then little
                    4 => new[] { 3, 2, 1, 0 }, // Little -> ring
                    _ => Array.Empty<int>()
                };
                foreach (var nearbyFinger in nearbyFingers) {
                    for (var segmentIndex = segment; segmentIndex >= 0; segmentIndex--) {
                        yield return GetFingerBone(nearbyFinger, segmentIndex);
                    }
                }

                var hand = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
                yield return hand;
                foreach (var fallback in GetFallbacks(hand)) {
                    yield return fallback;
                }
                yield break;
            }

            HumanBodyBones? parentFallback = requested switch {
                HumanBodyBones.Jaw or HumanBodyBones.LeftEye or HumanBodyBones.RightEye =>
                    HumanBodyBones.Head,
                HumanBodyBones.Head => HumanBodyBones.Neck,
                HumanBodyBones.Neck => HumanBodyBones.UpperChest,
                HumanBodyBones.UpperChest => HumanBodyBones.Chest,
                HumanBodyBones.Chest => HumanBodyBones.Spine,
                HumanBodyBones.Spine => HumanBodyBones.Hips,

                HumanBodyBones.LeftHand => HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightHand => HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftLowerArm => HumanBodyBones.LeftUpperArm,
                HumanBodyBones.RightLowerArm => HumanBodyBones.RightUpperArm,
                HumanBodyBones.LeftUpperArm => HumanBodyBones.LeftShoulder,
                HumanBodyBones.RightUpperArm => HumanBodyBones.RightShoulder,
                HumanBodyBones.LeftShoulder or HumanBodyBones.RightShoulder => HumanBodyBones.UpperChest,

                HumanBodyBones.LeftToes => HumanBodyBones.LeftFoot,
                HumanBodyBones.RightToes => HumanBodyBones.RightFoot,
                HumanBodyBones.LeftFoot => HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightFoot => HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftLowerLeg => HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.RightLowerLeg => HumanBodyBones.RightUpperLeg,
                HumanBodyBones.LeftUpperLeg or HumanBodyBones.RightUpperLeg => HumanBodyBones.Hips,
                _ => null
            };
            if (parentFallback != null) {
                yield return parentFallback.Value;
                foreach (var recursiveFallback in GetFallbacks(parentFallback.Value)) {
                    yield return recursiveFallback;
                }
            }
        }

        private static bool TryGetFinger(
            HumanBodyBones bone,
            out bool right,
            out int finger,
            out int segment
        ) {
            var value = (int)bone;
            var leftFirst = (int)HumanBodyBones.LeftThumbProximal;
            var leftLast = (int)HumanBodyBones.LeftLittleDistal;
            var rightFirst = (int)HumanBodyBones.RightThumbProximal;
            var rightLast = (int)HumanBodyBones.RightLittleDistal;

            right = value >= rightFirst && value <= rightLast;
            if (!right && (value < leftFirst || value > leftLast)) {
                finger = 0;
                segment = 0;
                return false;
            }

            var offset = value - (right ? rightFirst : leftFirst);
            finger = offset / 3;
            segment = offset % 3;
            return true;
        }
    }
}
