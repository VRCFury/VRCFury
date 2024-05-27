using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    public static class MotionExtensions {
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

        public static void MakeZeroLength(this Motion motion) {
            if (motion is AnimationClip clip) {
                clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                    if (curve.lengthInSeconds == 0) return (binding, curve, false);
                    return (binding, curve.GetFirst(), true);
                }));
                if (!clip.GetAllBindings().Any()) {
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve("__ignored", typeof(GameObject), "m_IsActive"),
                        AnimationCurve.Constant(0, 0, 0)
                    );
                }
            } else {
                foreach (var tree in new AnimatorIterator.Trees().From(motion)) {
                    tree.RewriteChildren(child => {
                        if (child.motion == null) {
                            child.motion = VrcfObjectFactory.Create<AnimationClip>();
                            child.motion.name = "Empty";
                        }
                        return child;
                    });
                }
                foreach (var c in new AnimatorIterator.Clips().From(motion)) {
                    c.MakeZeroLength();
                }
            }
        }
    }
}
