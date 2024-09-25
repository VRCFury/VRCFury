using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
        
        public static Motion GetLastFrame(this Motion motion) {
            var clone = motion.Clone();
            foreach (var clip in new AnimatorIterator.Clips().From(clone)) {
                clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                    if (curve.lengthInSeconds == 0) return (binding, curve, false);
                    return (binding, curve.GetLast(), true);
                }));
            }
            return clone;
        }

        public static AnimationClip FlattenAll(this Motion motion) {
            if (motion is AnimationClip c) return c.Clone();
            var flat = VrcfObjectFactory.Create<AnimationClip>();
            foreach (var clip in new AnimatorIterator.Clips().From(motion)) {
                flat.CopyFrom(clip);
            }
            return flat;
        }

        private static IList<AnimationClip> GetActiveClips(this Motion motion, HashSet<string> onParams) {
            if (motion is AnimationClip c) {
                return new [] { c };
            }
            if (motion is BlendTree tree) {
                if (tree.children.Any()) {
                    if (tree.blendType == BlendTreeType.Direct) {
                        return tree.children
                            .Where(child => onParams.Contains(child.directBlendParameter))
                            .SelectMany(child => child.motion.GetActiveClips(onParams))
                            .ToArray();
                    } else if (tree.blendType == BlendTreeType.Simple1D) {
                        if (onParams.Contains(tree.blendParameter)) {
                            return tree.children.OrderBy(child => child.threshold).Last().motion.GetActiveClips(onParams);
                        } else {
                            return tree.children.OrderBy(child => child.threshold).First().motion.GetActiveClips(onParams);
                        }
                    }
                }
            }
            return new AnimationClip[] { };
        }

        public static AnimationClip EvaluateMotion(this Motion motion, float fraction) {
            var onParams = new HashSet<string>() {
                // "IsLocal",
                // "IsOnFriendsList",
                VFBlendTreeDirect.AlwaysOneParam,
            };
            var output = VrcfObjectFactory.Create<AnimationClip>();
            output.name = $"{motion.name} (sampled at {Math.Round(fraction*100)}%)";
            foreach (var clip in motion.GetActiveClips(onParams)) {
                output.CopyFrom(clip.EvaluateClip(fraction * clip.GetLengthInSeconds()));
            }
            return output;
        }
    }
}
