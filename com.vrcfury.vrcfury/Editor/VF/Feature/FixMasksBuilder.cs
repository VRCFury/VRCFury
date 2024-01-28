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

        [FeatureBuilderAction(FeatureOrder.FixGestureFxConflict)]
        public void FixGestureFxConflict() {
            if (manager.GetAllUsedControllers().All(c => c.GetType() != VRCAvatarDescriptor.AnimLayerType.Gesture)) {
                // No customized gesture controller
                return;
            }

            var gesture = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
            var newFxLayers = new List<AnimatorControllerLayer>();
            var fx = manager.GetFx();

            bool IsMuscle(EditorCurveBinding b) {
                if (b.IsProxyBinding()) return true;
                return b.path == "" && b.type == typeof(Animator);
            }

            foreach (var layer in gesture.GetLayers()) {
                var hasMuscle = new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Any(IsMuscle);
                var hasNonMuscle = new AnimatorIterator.Clips().From(layer)
                    .SelectMany(clip => clip.GetAllBindings())
                    .Any(b => !IsMuscle(b));

                if (!hasNonMuscle) continue;

                var copyLayer = new AnimatorControllerLayer {
                    name = layer.name,
                    stateMachine = MutableManager.CopyRecursive(layer.stateMachine, false),
                    avatarMask = MutableManager.CopyRecursive(layer.mask, false),
                    blendingMode = layer.blendingMode,
                    defaultWeight = layer.weight
                };
                newFxLayers.Add(copyLayer);
                if (hasMuscle) {
                    foreach (var clip in new AnimatorIterator.Clips().From(layer)) {
                        clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                            if (IsMuscle(b)) return b;
                            return null;
                        }, false));
                    }
                    foreach (var clip in new AnimatorIterator.Clips().From(new VFLayer(null, copyLayer.stateMachine))) {
                        clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                            if (IsMuscle(b)) return null;
                            return b;
                        }, false));
                    }
                } else {
                    layer.Remove();
                }
            }

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
