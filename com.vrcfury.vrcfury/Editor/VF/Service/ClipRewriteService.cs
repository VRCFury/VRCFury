using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    internal class ClipRewriteService {
        [VFAutowired] private readonly AvatarManager manager;
        private readonly List<AnimationClip> additionalClips = new List<AnimationClip>();

        public void RewriteAllClips(AnimationRewriter rewriter) {
            foreach (var c in manager.GetAllUsedControllers()) {
                c.GetRaw().GetRaw().Rewrite(rewriter);
            }
            foreach (var clip in additionalClips) {
                clip.Rewrite(rewriter);
            }
        }
        
        /**
         * Note: Does not update audio clip source paths
         */
        public void ForAllClips(Action<AnimationClip> with) {
            var clips = new HashSet<AnimationClip>();
            foreach (var c in manager.GetAllUsedControllers()) {
                clips.UnionWith(c.GetClips());
            }
            clips.UnionWith(additionalClips);
            foreach (var clip in clips) {
                with(clip);
            }
        }
        
        public void AddAdditionalManagedClip(AnimationClip clip) {
            additionalClips.Add(clip);
        }
    }
}
