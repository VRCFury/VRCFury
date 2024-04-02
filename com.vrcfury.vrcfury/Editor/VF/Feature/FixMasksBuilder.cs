using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [VFService]
    public class FixMasksBuilder : FeatureBuilder {

        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder animatorLayerControlManager;

        private enum PropType {
            Muscle,
            Aap,
            Fx
        }

        [FeatureBuilderAction(FeatureOrder.FixGestureFxConflict)]
        public void FixGestureFxConflict() {
            if (manager.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }

            var gesture = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
            var newFxLayers = new List<AnimatorControllerLayer>();
            var fx = manager.GetFx();
            
            PropType GetPropType(EditorCurveBinding b) {
                if (b.IsProxyBinding()) return PropType.Muscle;
                if (b.path == "" && b.type == typeof(Animator)) {
                    if (gesture.GetRaw().GetParam(b.propertyName) != null) return PropType.Aap;
                    return PropType.Muscle;
                }
                return PropType.Fx;
            }

            var copyForFx = MutableManager.CopyRecursive(gesture.GetRaw().GetRaw(), false).layers;

            gesture.GetRaw().layers = gesture.GetRaw().layers.Select((layerForGesture,i) => {
                
                var propTypes = new AnimatorIterator.Clips().From(new VFLayer(null,layerForGesture.stateMachine))
                    .SelectMany(clip => clip.GetAllBindings())
                    .Select(GetPropType)
                    .ToImmutableHashSet();

                if (!propTypes.Contains(PropType.Fx) && !propTypes.Contains(PropType.Aap)) {
                    // Keep it only in gesture
                    return layerForGesture;
                }

                var layerForFx = copyForFx[i];
                newFxLayers.Add(layerForFx);
                animatorLayerControlManager.Alias(layerForGesture.stateMachine, layerForFx.stateMachine);
                
                // Remove fx bindings from the gesture copy
                foreach (var clip in new AnimatorIterator.Clips().From(new VFLayer(null,layerForGesture.stateMachine))) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                        if (GetPropType(b) != PropType.Fx) return b;
                        return null;
                    }, false));
                }
                if (layerForGesture.avatarMask != null) {
                    layerForGesture.avatarMask.AllowAllTransforms();
                }

                // Remove muscle control from the fx copy
                foreach (var clip in new AnimatorIterator.Clips().From(new VFLayer(null,layerForFx.stateMachine))) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                        if (GetPropType(b) != PropType.Muscle) return b;
                        return null;
                    }, false));
                }
                if (layerForFx.avatarMask.AllowsAllTransforms()) {
                    layerForFx.avatarMask = null;
                }

                if (propTypes.Contains(PropType.Muscle) || propTypes.Contains(PropType.Aap)) {
                    // We're keeping both layers
                    // Remove behaviours from the fx copy
                    AnimatorIterator.ForEachBehaviourRW(new VFLayer(null,layerForFx.stateMachine), (behaviour, add) => false);
                    return layerForGesture;
                } else {
                    // We're only keeping it in FX
                    // Delete it from Gesture
                    return null;
                }
            }).NotNull().ToArray();

            if (newFxLayers.Count > 0) {
                fx.GetRaw().layers = newFxLayers.Concat(fx.GetRaw().layers).ToArray();
                foreach (var p in gesture.GetRaw().parameters) {
                    fx.GetRaw().NewParam(p.name, p.type, n => {
                        n.defaultBool = p.defaultBool;
                        n.defaultFloat = p.defaultFloat;
                        n.defaultInt = p.defaultInt;
                    });
                }
            }
        }
        
        [FeatureBuilderAction(FeatureOrder.FixMasks)]
        public void FixMasks() {
            foreach (var layer in GetFx().GetLayers()) {
                // For any layers we added to FX without masks, give them the default FX mask
                if (layer.mask == null) {
                    layer.mask = AvatarMaskExtensions.DefaultFxMask();
                }
                
                // Remove redundant FX masks if they're not needed
                if (layer.mask.AllowsAllTransforms() && !layer.HasMuscles()) {
                    layer.mask = null;
                }
            }

            foreach (var c in manager.GetAllUsedControllers()) {
                var ctrl = c.GetRaw();

                AvatarMask expectedMask = null;
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    expectedMask = GetGestureMask(c);
                } else if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    expectedMask = GetFxMask(c);
                }

                var layer0 = ctrl.GetLayer(0);
                // If there are no layers, we still create a base layer because the VRCSDK freaks out if there is a
                //   controller with no layers
                // On FX, ALWAYS make an empty base layer, because for some reason transition times can break
                //   and animate immediately when performed within the base layer
                if (layer0 == null || layer0.mask != expectedMask || c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
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

        private AvatarMask GetFxMask(ControllerManager fx) {
            return null;
        }
    }
}
