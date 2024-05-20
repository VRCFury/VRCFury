using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    public class ClipRewriteService {
        [VFAutowired] private AvatarManager manager;
        private readonly List<AnimationClip> additionalClips = new List<AnimationClip>();
        
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
