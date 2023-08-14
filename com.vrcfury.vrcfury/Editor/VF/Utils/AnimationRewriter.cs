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
        public static AnimationRewriter RewriteBinding(Func<EditorCurveBinding, EditorCurveBinding?> rewrite, bool skipProxyBindings = true) {
            var output = RewriteCurve((binding, curve) => {
                var newBinding = rewrite(binding);
                if (newBinding == null) return (binding, null, false);
                return (newBinding.Value, curve, false);
            });
            output.skipProxyBindings = skipProxyBindings;
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
            output.skipProxyBindings = false;
            return output;
        }
        private CurveRewriter curveRewriter;
        private bool skipProxyBindings = true;
        private AnimationRewriter(CurveRewriter curveRewriter) {
            this.curveRewriter = curveRewriter;
        }

        private (EditorCurveBinding, FloatOrObjectCurve, bool) RewriteOne(EditorCurveBinding binding, FloatOrObjectCurve curve) {
            if (skipProxyBindings && binding.IsProxyBinding()) return (binding,curve,false);
            return curveRewriter(binding, curve);
        }
        public void Rewrite(AnimationClip clip) {
            var output = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                var (newBinding, newCurve, curveUpdated) = RewriteOne(binding, curve);
                if (newCurve == null) {
                    output.Add((binding, null));
                } else if (binding != newBinding || curve != newCurve || curveUpdated) {
                    output.Add((binding, null));
                    output.Add((newBinding, newCurve));
                }
            }
            clip.SetCurves(output);
        }

        public string RewritePath(string input) {
            var rewritten = RewriteOne(
                EditorCurveBinding.DiscreteCurve(input, typeof(Transform), "test"),
                new FloatOrObjectCurve(AnimationCurve.Constant(0,0,0))
            );
            if (rewritten.Item2 == null) return null;
            return rewritten.Item1.path;
        }
    }
}
