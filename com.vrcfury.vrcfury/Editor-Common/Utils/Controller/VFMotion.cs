using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal abstract class VFMotion {
        protected readonly Motion sourceRaw;

        protected VFMotion(Motion sourceRaw) {
            this.sourceRaw = sourceRaw;
        }

        internal static VFMotion Load(Motion raw, VFLoadContext context) {
            raw = context?.RewriteMotion?.Invoke(raw) ?? raw;
            if (raw == null) return null;
            if (context != null && context.Motions.TryGetValue(raw, out var existing)) {
                return existing;
            }

            VFMotion output;
            if (raw is AnimationClip clip) {
                output = VFClip.Load(clip, context);
            } else if (raw is BlendTree tree) {
                output = VFTree.Load(tree, context);
            } else {
                throw new Exception($"Unsupported motion type `{raw.GetType().Name}`");
            }
            if (context != null) {
                context.Motions[raw] = output;
            }
            return output;
        }

        public Motion GetSourceAsset() {
            return sourceRaw;
        }

        internal abstract VFMotion Clone(VFMotionCloneContext context = null);

        public Motion Save(VFGameObject bindingRoot, bool reuseSourceAssets = true) {
            if (bindingRoot == null) throw new ArgumentNullException(nameof(bindingRoot));
            return Save(new VFSaveContext(bindingRoot, reuseSourceAssets));
        }

        internal abstract Motion Save(VFSaveContext context);

        internal virtual VFBinding[] GetFloatBindings() {
            return new AnimatorIterator.Clips().From(this)
                .SelectMany(clip => clip?.GetFloatBindings() ?? Array.Empty<VFBinding>())
                .Distinct()
                .ToArray();
        }

        internal virtual void Rewrite(AnimationRewriter rewriter) {
        }

        internal virtual void RewriteParameters(Func<string, string> rewriteParamName) {
        }

        internal abstract bool IsStatic();
        internal abstract bool IsTwoState();
        internal abstract bool IsEmptyOrZeroLength();
        internal abstract VFMotion GetLastFrame(bool last = true);
        internal abstract VFClip FlattenAll();
        internal abstract VFClip EvaluateMotion(float fraction);
    }
}
