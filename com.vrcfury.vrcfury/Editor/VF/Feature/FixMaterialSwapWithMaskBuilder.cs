using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

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
            // TODO: Work on this
            return;
            
            // var nextInt = 1;
            // foreach (var c in manager.GetAllUsedControllers()) {
            //     foreach (var layer in c.GetLayers()) {
            //         CheckLayer(c, layer, ref nextInt);
            //     }
            // }
        }

        private void CheckLayer(ControllerManager c, AnimatorStateMachine layer, ref int nextInt) {
            var mask = c.GetMask(c.GetLayerId(layer));
            if (mask == null) return;
            if (mask.transformCount == 0) return;

            var bindingMatToParam = new Dictionary<(EditorCurveBinding,Object), VFABool>();
            foreach (var clip in new AnimatorIterator.Clips().From(layer)) {
                foreach (var binding in clip.GetObjectBindings()) {
                    if (!binding.propertyName.StartsWith("m_Materials.")) continue;
                    var curve = clip.GetObjectCurve(binding);
                    var allMatsInCurve = curve
                        .Select(frame => frame.value)
                        .Where(value => value != null)
                        .ToImmutableHashSet();
                    foreach (var mat in allMatsInCurve) {
                        if (!bindingMatToParam.TryGetValue((binding, mat), out var param)) {
                            param = c.NewBool($"transformMat{nextInt++}");
                            bindingMatToParam[(binding, mat)] = param;
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
                var directLayer = c.NewLayer($"{layer.name} - Mat Swaps");
                var directTree = c.NewBlendTree($"{layer.name} - Mat Swaps");
                directTree.blendType = BlendTreeType.Direct;
                directLayer.NewState("Direct").WithAnimation(directTree);
                foreach (var pair in bindingMatToParam) {
                    var (binding, mat) = pair.Key;
                    var param = pair.Value;
                    var clip = c.NewClip($"{layer.name} - Mat Swaps");
                    clip.SetObjectCurve(binding, new ObjectReferenceKeyframe[] {
                        new ObjectReferenceKeyframe() {
                            time = 0,
                            value = mat
                        }
                    });
                    directTree.AddDirectChild(param.Name(), clip);
                }
            }
        }
    }
}
