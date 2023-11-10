using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixMasksBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void Apply() {
            var allControllers = manager.GetAllUsedControllers().ToArray();

            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    expectedMask = GetFxMask(c, allControllers);
                }

                var layer0 = ctrl.GetLayer(0);
                // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a
                // controller with no layers
                if (layer0 == null || layer0.mask != expectedMask) {
                    c.EnsureEmptyBaseLayer().mask = expectedMask;
                }
            }
        }

        /**
         * We build the gesture base mask by unioning all the masks from the other layers.
         */
        private AvatarMask GetGestureMask(ControllerManager gesture) {
            var mask = AvatarMaskExtensions.Empty();
            foreach (var layer in gesture.GetLayers()) {
                if (layer.mask == null) throw new Exception("Gesture layer unexpectedly contains no mask");
                mask.UnionWith(layer.mask);
            }
            return mask;
        }

        private AvatarMask GetFxMask(ControllerManager fx, IEnumerable<ControllerManager> allControllers) {
            var gestureContainsTransform = allControllers
                .Where(c => c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture)
                .SelectMany(c => c.GetClips())
                .SelectMany(clip => clip.GetAllBindings())
                .Any(binding => binding.type == typeof(Transform));
            if (!gestureContainsTransform) return null;

            foreach (var layer in fx.GetLayers()) {
                var oldMask = layer.mask;
                
                // Direct blendtree layers don't need a mask because they're always WD on
                // and that doesn't break transforms on gesture because... unity reasons
                var isOnlyDirectTree = new AnimatorIterator.States()
                    .From(layer)
                    .All(state => state.motion is BlendTree tree && tree.blendType == BlendTreeType.Direct);
                if (isOnlyDirectTree) continue;

                var transformedPaths = new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .ToImmutableHashSet();

                var mask = AvatarMaskExtensions.Empty();
                mask.SetTransforms(transformedPaths);
                if (oldMask != null) {
                    mask.IntersectWith(oldMask);
                }

                layer.mask = mask;
            }

            return null;
        }
    }
}
