using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal class AnimationRewriter {
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
        public static AnimationRewriter RewriteObject(Func<Object,Object> rewrite) {
            return RewriteCurve((binding, curve) => {
                if (curve.IsFloat) return (binding, curve, false);
                var changed = false;
                var newCurve = curve.ObjectCurve.Select(frame => {
                    if (frame.value == null) return frame;
                    var newValue = rewrite(frame.value);
                    if (newValue != frame.value) {
                        frame.value = newValue;
                        changed = true;
                    }
                    return frame;
                }).ToArray();
                return (binding, newCurve, changed);
            });
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
            var changes = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            var beforeCurves = clip.GetAllCurves();
            foreach (var (binding,curve) in beforeCurves) {
                if (binding.path == "__vrcf_length") {
                    continue;
                }
                var (newBinding, newCurve, curveUpdated) = RewriteOne(binding, curve);
                if (newCurve == null) {
                    changes.Add((binding, null));
                } else if (binding != newBinding) {
                    changes.Add((binding, null));
                    changes.Add((newBinding, newCurve));
                } else if (curve != newCurve || curveUpdated) {
                    changes.Add((binding, newCurve));
                }
            }
            clip.SetCurves(changes);
            var afterCurves = clip.GetAllCurves();

            // Ensure we maintain the same length as previous
            var newLength = clip.GetLengthInSeconds();
            if (originalLength != newLength) {
                clip.SetLengthHolder(originalLength);
            }
            
            // Whether or not a clip has any bindings at all can actually have an impact, so we ensure that if all
            // bindings were removed, we always keep at least one
            if (beforeCurves.Any() && !afterCurves.Any()) {
                clip.SetLengthHolder(originalLength);
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
