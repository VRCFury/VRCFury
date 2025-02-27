using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class ClipRewriteService {
        [VFAutowired] private readonly ControllersService controllers;
        private readonly List<AnimationClip> additionalClips = new List<AnimationClip>();

        public void RewriteAllClips(AnimationRewriter rewriter) {
            foreach (var c in controllers.GetAllUsedControllers()) {
                c.Rewrite(rewriter);
            }
            foreach (var clip in additionalClips) {
                clip.Rewrite(rewriter);
            }
        }

        /**
         * Note: Does not update audio clip source paths
         */
        public ISet<AnimationClip> GetAllClips() {
            var clips = new HashSet<AnimationClip>();
            foreach (var c in controllers.GetAllUsedControllers()) {
                clips.UnionWith(c.GetClips());
            }
            clips.UnionWith(additionalClips);
            return clips;
        }
        
        public void AddAdditionalManagedClip(AnimationClip clip) {
            additionalClips.Add(clip);
        }
    }
}
