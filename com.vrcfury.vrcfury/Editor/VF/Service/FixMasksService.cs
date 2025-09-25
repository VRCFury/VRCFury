using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class FixMasksService {

        [VFAutowired] private readonly AnimatorLayerControlOffsetService animatorLayerControlManager;
        [VFAutowired] private readonly LayerSourceService layerSourceService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        [FeatureBuilderAction(FeatureOrder.FixGestureFxConflict)]
        public void FixGestureFxConflict() {
            if (controllers.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }

            var gesture = controllers.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
            var copyForFx = gesture.Clone();
            foreach (var to in copyForFx.layers.SelectMany(l => l.allBehaviours).OfType<VRCAnimatorLayerControl>()) {
                animatorLayerControlManager.Alias(to.GetCloneSource(), to);
            }

            foreach (var (layerForGesture, layerForFx) in gesture.layers.Zip(copyForFx.layers, (a,b) => (a,b))) {
                
                var propTypes = new AnimatorIterator.Clips().From(layerForGesture)
                    .SelectMany(clip => {
                        if (clip.IsProxyClip()) return new[]{ EditorCurveBindingType.Muscle };
                        return clip.GetAllBindings().Select(b => b.GetPropType());
                    })
                    .ToImmutableHashSet();

                if (!propTypes.Contains(EditorCurveBindingType.Fx) && !propTypes.Contains(EditorCurveBindingType.Aap)) {
                    // Keep it only in gesture
                    layerForFx.Remove();
                    continue;
                }

                animatorLayerControlManager.Alias(layerForGesture, layerForFx);
                layerSourceService.CopySource(layerForGesture, layerForFx);

                if (propTypes.Contains(EditorCurveBindingType.Muscle) || propTypes.Contains(EditorCurveBindingType.Aap)) {
                    // We're keeping both layers
                    // Remove behaviours from the fx copy
                    layerForFx.RewriteBehaviours(b => null);
                } else {
                    // We're only keeping it in FX
                    // Delete it from Gesture
                    layerForGesture.Remove();
                }

            }

            if (copyForFx.layers.Any()) {
                fx.TakeOwnershipOf(copyForFx, putOnTop: true, prefix: false);
            }

            // This clip cleanup must happen after the merge is all finished,
            // because otherwise `propTypes` above could be wrong for some layers that use shared clips
            // if we cleanup while iterating through the layers
            
            // Remove fx bindings from the gesture copy
            foreach (var clip in new AnimatorIterator.Clips().From(gesture)) {
                clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                    if (b.GetPropType() == EditorCurveBindingType.Fx) return null;
                    return b;
                }));
            }

            foreach (var layerForGesture in gesture.GetLayers()) {
                if (layerForGesture.mask != null) {
                    layerForGesture.mask.AllowAllTransforms();
                }
            }

            // Remove muscle control from the fx copy
            foreach (var clip in new AnimatorIterator.Clips().From(fx)) {
                clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                    if (b.GetPropType() == EditorCurveBindingType.Muscle) return null;
                    return b;
                }));
            }

            foreach (var layerForFx in fx.GetLayers()) {
                if (layerForFx.mask != null && layerForFx.mask.AllowsAllTransforms()) {
                    layerForFx.mask = null;
                }
            }
        }
        
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void FixMasks() {
            foreach (var layer in fx.GetLayers()) {
                // Remove redundant FX masks if they're not needed
                if (layer.mask != null && layer.mask.AllowsAllTransforms()) {
                    layer.mask = null;
                }
            }

            foreach (var c in controllers.GetAllUsedControllers()) {
                var ctrl = c;

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                }

                var layer0 = ctrl.GetLayer(0);
                var createEmptyBaseLayer = false;
                if (layer0 == null) {
                    // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a controller with no layers
                    createEmptyBaseLayer = true;
                } else if (layer0.mask != expectedMask) {
                    if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX || c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                        // If the mask on layer 0 doesn't match what the base mask needs to be, we need to correct it by making a new base layer
                        // (Note: VRC only respects the base mask on gesture and fx)
                        createEmptyBaseLayer = true;
                    } else {
                        // If a base layer has humanoid animations AND a mask, it does weird things.
                        // For example, if the layer 0 in the Additive controller contains a full human pose, and there's a mask limiting it to only the jaw bone,
                        // it can still impact the hip offset. This doesn't happen if it's on layer 1+. So we must ensure that there's never content with a mask on layer 0 in these cases.
                        createEmptyBaseLayer = true;
                    }
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX && (layer0.hasSubMachines || layer0.allStates.Count > 1)) {
                    // On FX, do not allow a layer with transitions to live as the base layer, because for some reason transition times can break
                    //   and animate immediately when performed within the base layer
                    createEmptyBaseLayer = true;
                }

                if (createEmptyBaseLayer) {
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
    }
}
