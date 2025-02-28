using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal static class AnimatorControllerExtensions {
        public static void Rewrite(this VFController c, AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.mask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }
            
            // Rewrite VRCAnimatorPlayAudio
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
            foreach (var audio in c.layers.SelectMany(l => l.allBehaviours).OfType<VRCAnimatorPlayAudio>()) {
                audio.SourcePath = rewriter.RewritePath(audio.SourcePath);
            }
#endif
        }
    }
}
