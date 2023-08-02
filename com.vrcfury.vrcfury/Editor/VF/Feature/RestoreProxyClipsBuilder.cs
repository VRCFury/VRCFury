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
                foreach (var layer in controller.GetManagedLayers()) {
                    foreach (var state in new AnimatorIterator.States().From(controller.GetRaw())) {
                        ApplyToState(state);
                    }
                }
            }
        }

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
            var proxies = clip.CollapseProxyBindings();
            if (proxies.Count > 0) {
                return proxies[0].Item1;
            }
            return clip;
        }
    }
}
