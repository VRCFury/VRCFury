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
            return RewriteCurve((binding, curve) => {
                var newBinding = rewrite(binding);
                if (newBinding == null) return (binding, null, false);
                return (newBinding.Value, curve, false);
            });
        }
        public static AnimationRewriter RewriteCurve(CurveRewriter rewrite) {
            return new AnimationRewriter(rewrite);
        }
        public static AnimationRewriter DeleteAllBindings() {
            return RewriteCurve((b, c) => (b, null, false));
        }
        public static AnimationRewriter Combine(params AnimationRewriter[] rewriters) {
            if (rewriters.Length == 1) return rewriters[0];
            return RewriteCurve((b, c) => {
                var binding = b;
                var curve = c;
                var curveUpdated = false;
                foreach (var rewriter in rewriters) {
                    if (curve == null) break;
                    bool u;
                    (binding,curve,u) = rewriter.curveRewriter(binding, curve);
                    curveUpdated |= u;
                }
                return (binding,curve,curveUpdated);
            });
        }
        private CurveRewriter curveRewriter;
        private AnimationRewriter(CurveRewriter curveRewriter) {
            this.curveRewriter = curveRewriter;
        }
        
        public void Rewrite(AnimationClip clip) {
            var output = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (binding.IsProxyBinding()) continue;
                var (newBinding,newCurve,curveUpdated) = curveRewriter(binding, curve);
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
            var rewritten = curveRewriter(
                EditorCurveBinding.DiscreteCurve(input, typeof(Transform), "test"),
                null
            );
            if (rewritten.Item2 == null) return null;
            return rewritten.Item1.path;
        }
    }
}
