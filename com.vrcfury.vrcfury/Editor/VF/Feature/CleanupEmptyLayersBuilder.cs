using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    /**
     * This builder removes animation bindings that do nothing, for cleanliness, to save space,
     * and to avoid including assets (like materials) that are referenced in animations but not actually used
     */
    public class CleanupEmptyLayersBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.CleanupEmptyLayers)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var removedBindings = new List<string>();

                // Delete bindings targeting things that don't exist
                foreach (var clip in new AnimatorIterator.Clips().From(c.GetRaw())) {
                    var originalLength = clip.length;
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.path == "__vrcf_length") {
                            return null;
                        }
                        if (!binding.IsValid(avatarObject)) {
                            removedBindings.Add($"{binding.PrettyString()} from {clip.name}");
                            return null;
                        }
                        return binding;
                    }));
                    var newLength = clip.length;
                    if (originalLength != newLength) {
                        clip.SetFloatCurve(
                            EditorCurveBinding.FloatCurve("__vrcf_length", typeof(GameObject), "m_IsActive"),
                            AnimationCurve.Constant(0, originalLength, 0)
                        );
                    }
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
                        .Any(clip => !ClipBuilderService.IsEmptyMotion(clip, avatarObject));
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
