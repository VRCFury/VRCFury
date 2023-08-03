using UnityEngine;

namespace VF.Utils {
    public static class AnimationRewriterExtensions {
        public static void Rewrite(this AnimationClip clip, AnimationRewriter rewriter) {
            rewriter.Rewrite(clip);
        }
    }
}
