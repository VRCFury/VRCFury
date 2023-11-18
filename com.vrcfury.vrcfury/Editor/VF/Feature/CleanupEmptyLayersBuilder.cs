using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

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
                foreach (var state in new AnimatorIterator.States().From(c.GetRaw())) {
                    var clip = state.motion as AnimationClip;
                    if (clip == null) continue;

                    var originalLength = clip.length;
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.path == "__vrcf_length") {
                            return null;
                        }
                        if (binding.path == "__vrcf_global_param") {
                            var driver = state.AddStateMachineBehaviour(typeof(VRCAvatarParameterDriver)) as VRCAvatarParameterDriver;
                            var p = new VRC_AvatarParameterDriver.Parameter();
                            p.name = binding.propertyName;
                            p.type = VRC_AvatarParameterDriver.ChangeType.Set;
                            var value = AnimationUtility.GetEditorCurve(clip, binding)[0].value;
                            p.value = value;
                            driver.parameters.Add(p);
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
