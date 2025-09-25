using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class AnimationCurveExtensions {
        /**
         * AnimationCurve.CopyFrom and AnimationCurve.keys strips NaN values, which some systems use to
         * do tricky mesh culling. We have to handle all these keys specially to maintain them.
         */
        public static AnimationCurve Clone(this AnimationCurve curve) {
            var keyCount = curve.keys.Length;
            
            var copy = new AnimationCurve();
#if UNITY_2022_1_OR_NEWER
            copy.CopyFrom(curve);
#else
            copy.keys = curve.keys.ToArray();
            copy.preWrapMode = curve.preWrapMode;
            copy.postWrapMode = curve.postWrapMode;
#endif
            if (copy.keys.Length != keyCount) {
                foreach (var key in curve.keys) {
                    if (float.IsNaN(key.value)) {
                        copy.AddKey(key.time, key.value);
                    }
                }
                if (copy.keys.Length != keyCount) {
                    throw new Exception("Keys are missing from AnimationCurve after cloning");
                }
            }

            return copy;
        }

        public static void MutateKeys(this AnimationCurve curve, Func<Keyframe, Keyframe> mutator) {
            var keys = curve.keys;
            var keyCount = keys.Length;
            
            var nanTimes = new List<float>();
            var newKeys = keys.SelectMany(key => {
                key = mutator(key);
                if (float.IsNaN(key.value)) {
                    nanTimes.Add(key.time);
                    return new Keyframe[] { };
                } else {
                    return new[] { key };
                }
            }).ToArray();

            curve.keys = newKeys;
            foreach (var nanTime in nanTimes) {
                curve.AddKey(nanTime, float.NaN);
            }
            if (curve.keys.Length != keyCount) {
                throw new Exception("AnimationCurve key count changed during mutation");
            }
        }
    }
}
