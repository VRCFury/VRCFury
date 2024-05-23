using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public class AnimationRewriter {
        public delegate (EditorCurveBinding, FloatOrObjectCurve, bool) CurveRewriter(EditorCurveBinding binding, FloatOrObjectCurve curve);
        public static AnimationRewriter RewritePath(Func<string, string> rewrite) {
            return RewriteBinding(binding => {
                var newPath = rewrite(binding.path);
                if (newPath == null) return null;
                if (newPath == binding.path) return binding;
                var newBinding = binding;
                newBinding.path = newPath;
                return newBinding;
            });
        }
        public static AnimationRewriter RewriteBinding(Func<EditorCurveBinding, EditorCurveBinding?> rewrite) {
            var cache = new Dictionary<EditorCurveBinding, EditorCurveBinding?>();
            var output = RewriteCurve((binding, curve) => {
                if (!cache.TryGetValue(binding, out var newBinding)) {
                    newBinding = rewrite(binding);
                    cache[binding] = newBinding;
                }
                if (newBinding == null) return (binding, null, false);
                return (newBinding.Value, curve, false);
            });
            return output;
        }
        public static AnimationRewriter RewriteCurve(CurveRewriter rewrite) {
            return new AnimationRewriter(rewrite);
        }
        public static AnimationRewriter DeleteAllBindings() {
            return RewriteCurve((b, c) => (b, null, false));
        }
        public static AnimationRewriter Combine(params AnimationRewriter[] rewriters) {
            if (rewriters.Length == 1) return rewriters[0];
            var output = RewriteCurve((b, c) => {
                var binding = b;
                var curve = c;
                var curveUpdated = false;
                foreach (var rewriter in rewriters) {
                    if (curve == null) break;
                    bool u;
                    (binding,curve,u) = rewriter.RewriteOne(binding, curve);
                    curveUpdated |= u;
                }
                return (binding,curve,curveUpdated);
            });
            return output;
        }
        private readonly CurveRewriter curveRewriter;
        private AnimationRewriter(CurveRewriter curveRewriter) {
            this.curveRewriter = curveRewriter;
        }

        private (EditorCurveBinding, FloatOrObjectCurve, bool) RewriteOne(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            return curveRewriter(binding, curve);
        }
        public void Rewrite(AnimationClip clip) {
            var originalLength = clip.GetLengthInSeconds();
            var output = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (binding.path == "__vrcf_length") {
                    continue;
                }
                var (newBinding, newCurve, curveUpdated) = RewriteOne(binding, curve);
                if (newCurve == null) {
                    output.Add((binding, null));
                } else if (binding != newBinding || curve != newCurve || curveUpdated) {
                    output.Add((binding, null));
                    output.Add((newBinding, newCurve));
                }
            }
            clip.SetCurves(output);
            var newLength = clip.GetLengthInSeconds();
            if (originalLength != newLength) {
                clip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("__vrcf_length", typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, originalLength, 0)
                );
            }
        }

        public string RewritePath(string input) {
            var rewritten = RewriteOne(
                EditorCurveBinding.FloatCurve(input, typeof(Transform), "test"),
                0
            );
            if (rewritten.Item2 == null) return null;
            return rewritten.Item1.path;
        }
    }
}
