using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class MotionExtensions {
        public static bool IsStatic(this Motion motion) {
            return new AnimatorIterator.Clips().From(motion).All(IsStatic);
        }

        private static bool IsStatic(AnimationClip clip) {
            if (clip.IsProxyClip()) return false;
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (curve.IsFloat) {
                    var keys = curve.FloatCurve.keys;
                    if (keys.All(key => key.time != 0)) return false;
                    if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
                } else {
                    var keys = curve.ObjectCurve;
                    if (keys.All(key => key.time != 0)) return false;
                    if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
                }
            }
            return true;
        }

        public static bool IsEmptyOrZeroLength(this Motion motion) {
            return new AnimatorIterator.Clips().From(motion).All(clip => clip.GetLengthInSeconds() == 0 || clip.GetAllBindings().Length == 0);
        }

        public static bool HasValidBinding(this Motion motion, VFGameObject avatarRoot) {
            return new AnimatorIterator.Clips().From(motion)
                .Any(clip => HasValidBinding(clip, avatarRoot));
        }

        private static bool HasValidBinding(AnimationClip clip, VFGameObject avatarRoot) {
            return clip.GetAllBindings()
                .Any(binding => binding.IsValid(avatarRoot));
        }
    }
}
