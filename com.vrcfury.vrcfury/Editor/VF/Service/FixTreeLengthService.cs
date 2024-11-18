using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * The "length" (in seconds) of a DBT is... weird. In most cases, the length of the tree is equal to the sum of
     * (clip length * weight / scale) of all involved clips. However, if the TOTAL weight of all clips is less than 1,
     * the final result is multiplied by (1 / total weight).
     *
     * If the final result is 0, then unity falls back to 1 second.
     *
     * This is, quite honestly, never the result that we ever want. We typically want trees to last the duration
     * of the longest clip they contain, regardless of if it is on or off.
     *
     * We can do this finding all affected trees, setting their speed to a really high value (so they don't impact the final length),
     * then wrapping them with another 1 speed clip that defines the actual length.
     */
    [VFService]
    internal class FixTreeLengthService {
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        
        [FeatureBuilderAction(FeatureOrder.FixTreeLength)]
        public void Apply() {
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw()).Where(VFLayer.Created)) {
                if (state.timeParameterActive) {
                    // States using motion time don't need "fixed" because the length of the state doesn't matter!
                    continue;
                }
                if (!(state.motion is BlendTree tree)) {
                    continue;
                }

                var clips = new AnimatorIterator.Clips().From(tree);
                var nonZeroClipLengths = clips
                    .Select(clip => clip.GetLengthInSeconds())
                    .Where(seconds => seconds != 0)
                    .ToArray();
                if (nonZeroClipLengths.Length >= 2) {
                    var maxLen = nonZeroClipLengths.Max();
                    foreach (var subtree in new AnimatorIterator.Trees().From(tree)) {
                        subtree.RewriteChildren(child => {
                            if (child.motion is AnimationClip) child.timeScale = 1_000_000_000;
                            return child;
                        });
                    }
                    var wrapper = VFBlendTreeDirect.Create($"{tree.name} (Length Fixed)");
                    var lenClip = clipFactory.NewClip($"{tree.name} Length", false);
                    lenClip.SetLengthHolder(maxLen);
                    wrapper.Add(lenClip);
                    wrapper.Add(tree);
                    state.motion = wrapper;
                }
            }
        }
    }
}
