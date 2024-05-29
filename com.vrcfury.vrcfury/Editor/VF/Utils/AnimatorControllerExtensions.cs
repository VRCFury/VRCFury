using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal static class AnimatorControllerExtensions {
        public static void Rewrite(this AnimatorController c, AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.avatarMask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }
            
            // Rewrite VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
            foreach (var b in new AnimatorIterator.Behaviours().From(c)) {
                if (b is VRCAnimatorPlayAudio audio) {
                    audio.SourcePath = rewriter.RewritePath(audio.SourcePath);
                }
            }
#endif
        }
    }
}
