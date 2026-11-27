using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
                var frames = curve.ObjectCurve;
                ObjectReferenceKeyframe[] updated = null;
                for (var i = 0; i < frames.Length; i++) {
                    var frame = frames[i];
                    if (frame.value == null) continue;
                    var newValue = rewrite(frame.value);
                    if (newValue == frame.value) continue;
                    updated ??= frames.ToArray();
                    frame.value = newValue;
                    updated[i] = frame;
                }
                return updated == null ? (binding, curve, false) : (binding, updated, true);
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
