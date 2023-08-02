using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;

namespace VF.Feature {
    /** Replaces VRCFury's Proxy bindings with the real vrchat proxy clips */
    public class RestoreProxyClipsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.RestoreProxyClips)]
        public void Apply() {
            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var state in new AnimatorIterator.States().From(controller.GetRaw())) {
                    ApplyToState(state);
                }
            }
        }

        private readonly Dictionary<AnimationClip, AnimationClip> cache
            = new Dictionary<AnimationClip, AnimationClip>();

        private void ApplyToState(AnimatorState state) {
            if (state.motion is AnimationClip clip) {
                state.motion = CheckClip(clip);
                return;
            }
            
            foreach (var tree in new AnimatorIterator.Trees().From(state)) {
                tree.children = tree.children.Select(child => {
                    if (child.motion is AnimationClip cl) {
                        child.motion = CheckClip(cl);
                    }
                    return child;
                }).ToArray();
            }
        }

        private AnimationClip CheckClip(AnimationClip clip) {
            if (cache.TryGetValue(clip, out var cached)) {
                return cached;
            }

            var replacementClip = clip;
            var proxies = clip.CollapseProxyBindings();
            if (proxies.Count > 0) {
                replacementClip = proxies[0].Item1;
            }

            cache[clip] = replacementClip;
            return replacementClip;
        }
    }
}
