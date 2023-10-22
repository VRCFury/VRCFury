using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {
    /**
     * If a mask is used on a layer, it prevents material swap animations on slot 1+ from working within the same layer.
     * (and slot 0 only works if the transform is enabled in the mask)
     * This builder fixes that issue by substituting the material swaps with animator params,
     * which in-turn trigger the material swaps using a direct blendtree.
     */
    public class FixMaterialSwapWithMaskBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixMaterialSwapWithMask)]
        public void Apply() {
            var nextInt = 1;
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var layer in c.GetLayers()) {
                    CheckLayer(c, layer, ref nextInt);
                }
            }
        }

        private void CheckLayer(ControllerManager c, VFLayer layer, ref int nextInt) {
            var mask = layer.mask;
            if (mask == null) return;
            if (mask.AllowsAllTransforms()) return;

            var bindingMatToParam = new Dictionary<(EditorCurveBinding,Object), VFAFloat>();
            foreach (var clip in new AnimatorIterator.Clips().From(layer)) {
                foreach (var binding in clip.GetObjectBindings()) {
                    if (!binding.propertyName.StartsWith("m_Materials.")) continue;
                    var curve = clip.GetObjectCurve(binding);
                    var allMatsInCurve = curve
                        .Select(frame => frame.value)
                        .Where(value => value != null)
                        .ToImmutableHashSet();
                    foreach (var mat in allMatsInCurve) {
                        var normalizedBinding = binding.Normalize();
                        if (!bindingMatToParam.TryGetValue((normalizedBinding, mat), out var param)) {
                            param = c.NewFloat($"transformMat{nextInt++}");
                            bindingMatToParam[(normalizedBinding, mat)] = param;
                        }

                        var newCurve = curve
                            .Select(frame => new Keyframe(frame.time, frame.value == mat ? 1 : 0))
                            .ToArray();
                        var newBinding = EditorCurveBinding.DiscreteCurve("", typeof(Animator), param.Name());
                        clip.SetFloatCurve(newBinding, new AnimationCurve(newCurve));
                    }
                    clip.SetObjectCurve(binding, null);
                }
            }

            if (bindingMatToParam.Count > 0) {
                // NOTE: We place this just BEFORE the existing layer, because if we place it after, it takes
                // 1 extra frame for the animated parameter change to be reflected in the direct tree. If we put it before (above)
                // then it is reflected on the same frame it is animated, which is what we want.
                var directLayer = c.NewLayer($"{layer.name} - Mat Swaps", layer.GetLayerId());
                var directTree = c.NewBlendTree($"{layer.name} - Mat Swaps");
                directTree.blendType = BlendTreeType.Direct;
                directLayer.NewState("Direct").WithAnimation(directTree);
                directLayer.NewState(
                    "VRCFury created this layer because the layer below this one changes material slots AND contains a mask\n\n" +
                    "Material slots >0 cannot be animated in layers containing a mask, so by moving the material swaps to this separate layer, that issue is avoided.");
                foreach (var pair in bindingMatToParam) {
                    var (binding, mat) = pair.Key;
                    var param = pair.Value;
                    var clip = c.NewClip($"{layer.name} - Mat Swaps");
                    clip.SetObjectCurve(binding, new [] {
                        new ObjectReferenceKeyframe {
                            time = 0,
                            value = mat
                        }
                    });
                    directTree.Add(param, clip);
                }
            }
        }
    }
}
