using System.Collections.Generic;
using UnityEngine;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Keeps tabs on all clips in the entire build. Not the same as all clips in the controllers,
     * since sometimes clips are stored separately temporarily, and then used later.
     */
    [VFService]
    internal class AllClipsService {
        [VFAutowired] private readonly ControllersService controllers;
        private readonly List<VFClip> additionalClips = new List<VFClip>();

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
        public ISet<VFClip> GetAllClips() {
            var clips = new HashSet<VFClip>();
            foreach (var c in controllers.GetAllUsedControllers()) {
                clips.UnionWith(c.GetClips());
            }
            clips.UnionWith(additionalClips);
            return clips;
        }

        public void AddAdditionalManagedClip(VFClip clip) {
            if (clip == null) return;
            additionalClips.Add(clip);
        }
    }
}
