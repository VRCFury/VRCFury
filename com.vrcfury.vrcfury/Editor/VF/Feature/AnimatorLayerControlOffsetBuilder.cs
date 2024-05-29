using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
    internal class AnimatorLayerControlOffsetBuilder : FeatureBuilder {
        private readonly VFMultimapList<VRCAnimatorLayerControl, AnimatorStateMachine> mapping
            = new VFMultimapList<VRCAnimatorLayerControl, AnimatorStateMachine>();
        
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

            var debugLog = new List<string>();

            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var l in c.GetLayers()) {
                    AnimatorIterator.ForEachBehaviourRW(l, (b, add) => {
                        if (!(b is VRCAnimatorLayerControl control)) return true;
                        var targetLayers = mapping.Get(control);
                        if (targetLayers.Count == 0) {
                            debugLog.Add("Removing invalid AnimatorLayerControl (not found in mapping??) " + b);
                            return false;
                        }
                        foreach (var targetLayer in targetLayers) {
                            if (!smToTypeAndNumber.TryGetValue(targetLayer, out var pair)) {
                                debugLog.Add("Removing invalid AnimatorLayerControl (target sm has disappeared) " + b);
                                continue;
                            }

                            var copy = (VRCAnimatorLayerControl)add(typeof(VRCAnimatorLayerControl));
                            EditorUtility.CopySerialized(control, copy);
                            var (newType, newI) = pair;
                            var newCastedType = VRCFEnumUtils.Parse<VRC_AnimatorLayerControl.BlendableLayer>(
                                VRCFEnumUtils.GetName(newType));
                            debugLog.Add($"Rewriting {b} from {control.playable}:{control.layer} to {newCastedType}:{newI}");
                            copy.playable = newCastedType;
                            copy.layer = newI;
                        }
                        return false;
                    });
                }
            }
            
            Debug.Log("Animator Layer Control Offset Builder Report:\n" + string.Join("\n", debugLog));
        }

        public void RegisterControllerSet(IEnumerable<(VRCAvatarDescriptor.AnimLayerType, VFController)> _set) {
            var set = _set.ToArray();
            foreach (var (type, controller) in set) {
                foreach (var layer in controller.GetLayers()) {
                    AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
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
            mapping.Put(control, targetSm);
        }

        public void Alias(AnimatorStateMachine oldTargetSm, AnimatorStateMachine newTargetSm) {
            foreach (var key in mapping.GetKeys()) {
                if (mapping.Get(key).Contains(oldTargetSm)) {
                    mapping.Put(key, newTargetSm);
                }
            }
        }
        
        public void Alias(VRCAnimatorLayerControl from, VRCAnimatorLayerControl to) {
            foreach (var sm in mapping.Get(from)) {
                mapping.Put(to, sm);
            }
        }

        public bool IsLayerTargeted(AnimatorStateMachine sm) {
            return mapping.ContainsValue(sm);
        }
    }
}
