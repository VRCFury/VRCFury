using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;

namespace VF.Feature {
    public class CleanupEmptyLayersBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupEmptyLayers)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var removedBindings = new List<string>();

                // Delete bindings targeting things that don't exist
                foreach (var clip in new AnimatorIterator.Clips().From(c.GetRaw())) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (!binding.IsValid(avatarObject)) {
                            removedBindings.Add($"{binding.PrettyString()} from {clip.name}");
                            return null;
                        }
                        return binding;
                    }));
                }

                if (removedBindings.Count > 0) {
                    Debug.LogWarning(
                        $"Removed {removedBindings.Count} properties from animation clips that targeted objects that do not exist:\n" +
                        string.Join("\n", removedBindings));
                }

                // Delete empty layers
                foreach (var (layer, i) in c.GetLayers().Select((l,i) => (l,i))) {
                    if (i == 0) continue;
                    var hasNonEmptyClip = new AnimatorIterator.Clips().From(layer)
                        .Any(clip => !ClipBuilder.IsEmptyMotion(clip, avatarObject));
                    var hasBehaviour = new AnimatorIterator.Behaviours().From(layer)
                        .Any();

                    if (!hasNonEmptyClip && !hasBehaviour) {
                        Debug.LogWarning($"Removing layer {layer.name} from {c.GetType()} because it doesn't do anything");
                        c.RemoveLayer(layer);
                    }
                }
            }
        }
    }
}
