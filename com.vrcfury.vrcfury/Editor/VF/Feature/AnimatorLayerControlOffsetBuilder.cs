using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * This builder is responsible for correcting any AnimatorLayerControl behaviours in
     * the animators which may have been broken by VRCFury adding / deleting layers
     * while doing other things.
     */
    public class AnimatorLayerControlOffsetBuilder : FeatureBuilder {
        private Dictionary<VRCAnimatorLayerControl, AnimatorStateMachine> mapping
            = new Dictionary<VRCAnimatorLayerControl, AnimatorStateMachine>();
        
        [FeatureBuilderAction(FeatureOrder.AnimatorLayerControlRecordBase)]
        public void RecordBase() {
            RegisterControllerSet(manager.GetAllUsedControllers().Select(c => (c.GetType(), c.GetRaw())));
        }

        [FeatureBuilderAction(FeatureOrder.AnimatorLayerControlFix)]
        public void Fix() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var layer0 = c.GetRaw().GetLayer(0);
                if (layer0 != null && mapping.ContainsValue(layer0)) {
                    // Something is trying to drive the base layer!
                    // Since this is impossible, we have to insert another layer above it to take its place
                    c.EnsureEmptyBaseLayer();
                }
            }

            var smToTypeAndNumber = new Dictionary<AnimatorStateMachine, (VRCAvatarDescriptor.AnimLayerType, int)>();
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var (i,l) in c.GetLayers().Select((l,i) => (i,l))) {
                    smToTypeAndNumber[l] = (c.GetType(), i);
                }
            }

            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var l in c.GetLayers()) {
                    AnimatorIterator.ForEachBehaviourRW(l, (b, add) => {
                        if (!(b is VRCAnimatorLayerControl control)) return true;
                        if (!mapping.TryGetValue(control, out var targetSm)) {
                            Debug.LogError("Removing invalid AnimatorLayerControl (not found in mapping??) " + b);
                            return false;
                        }
                        if (!smToTypeAndNumber.TryGetValue(targetSm, out var pair)) {
                            Debug.LogError("Removing invalid AnimatorLayerControl (target sm has disappeared) " + b);
                            return false;
                        }
                        var (newType, newI) = pair;
                        var newCastedType = VRCFEnumUtils.Parse<VRC_AnimatorLayerControl.BlendableLayer>(
                            VRCFEnumUtils.GetName(newType));
                        Debug.LogWarning($"Rewriting {b} from {control.playable}:{control.layer} to {newCastedType}:{newI}");
                        control.playable = newCastedType;
                        control.layer = newI;
                        return true;
                    });
                }
            }
        }

        public void RegisterControllerSet(IEnumerable<(VRCAvatarDescriptor.AnimLayerType, VFController)> set) {
            foreach (var (type, controller) in set) {
                foreach (var layer in controller.layers) {
                    AnimatorIterator.ForEachBehaviourRW(layer.stateMachine, (b, add) => {
                        if (b is VRCAnimatorLayerControl control) {
                            var targetController = set
                                .Where(tuple =>
                                    VRCFEnumUtils.GetName(tuple.Item1) == VRCFEnumUtils.GetName(control.playable))
                                .Select(tuple => tuple.Item2)
                                .FirstOrDefault();
                            if (targetController == null) return false;
                            if (control.layer < 0 || control.layer >= targetController.layers.Length) return false;
                            var targetSm = targetController.layers[control.layer].stateMachine;
                            Register(control, targetSm);
                        }

                        return true;
                    });
                }
            }
        }
        
        public void Register(VRCAnimatorLayerControl control, AnimatorStateMachine targetSm) {
            mapping[control] = targetSm;
        }
    }
}