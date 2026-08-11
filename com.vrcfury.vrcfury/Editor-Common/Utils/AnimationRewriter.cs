using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal class AnimationRewriter {
        public delegate (VFBinding, FloatOrObjectCurve, bool) CurveRewriter(VFBinding binding, FloatOrObjectCurve curve);

        public static AnimationRewriter RewriteBinding(Func<VFBinding, VFBinding?> rewrite) {
            var output = RewriteCurve((binding, curve) => {
                var newBinding = rewrite(binding);
                if (!newBinding.HasValue) return (binding, null, false);
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

        private (VFBinding, FloatOrObjectCurve, bool) RewriteOne(VFBinding binding, FloatOrObjectCurve curve) {
            return curveRewriter(binding, curve);
        }

        internal (VFBinding, FloatOrObjectCurve, bool) RewriteOneForLoaded(VFBinding binding, FloatOrObjectCurve curve) {
            return RewriteOne(binding, curve);
        }

        public void Rewrite(VFClip clip) {
            clip?.Rewrite(this);
        }
    }
}
