using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder<FixWriteDefaults> {
        [FeatureBuilderAction((int)FeatureOrder.FixWriteDefaults)]
        public void Apply() {
            // Ensure all controllers have an empty non-masked base layer, so they don't mask away
            // our vrcfury changes on accident
            foreach (var t in manager.GetAllUsedControllersRaw()) {
                var type = t.Item1;
                var controller = t.Item2;
                var needsFix = controller.layers.Length == 0
                               || controller.layers[0].stateMachine.states.Length > 0
                               || controller.layers[0].avatarMask != null;
                if (needsFix && type != VRCAvatarDescriptor.AnimLayerType.Gesture) {
                    var managed = manager.GetController(type);
                    managed.NewLayer("Base", 0);
                }
            }
            
            if (allFeaturesInRun.Any(f => f is MakeWriteDefaultsOff)) {
                MakeWriteDefaultsOff(true);
                return;
            }
            
            var useWriteDefaults = DetectExistingWriteDefaults();
            if (!useWriteDefaults) {
                Debug.Log("Detected 'Write Defaults Off', adjusting VRCFury states to use it too.");
                MakeWriteDefaultsOff(false);
            } else {
                // Usually the VRCF layers will all have writeDefaults = on by default, but some won't (like full controllers)
                foreach (var controller in manager.GetAllTouchedControllers()) {
                    foreach (var layer in controller.GetManagedLayers()) {
                        AnimatorIterator.ForEachState(layer, state => state.writeDefaultValues = true);
                    }
                }
            }
        }
        
        private void MakeWriteDefaultsOff(bool applyToUnmanagedLayers) {
            var missingStates = new List<string>();
            foreach (var controller in applyToUnmanagedLayers ? manager.GetAllUsedControllers() : manager.GetAllTouchedControllers()) {
                var defaultClip = manager.GetClipStorage().NewClip("Defaults");
                var defaultLayer = controller.NewLayer("Defaults", 1);
                defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
                foreach (var layer in controller.GetManagedLayers()) {
                    MakeWriteDefaultsOff(layer, defaultClip, manager.GetClipStorage().GetNoopClip(), avatarObject, missingStates);
                }
                if (applyToUnmanagedLayers) {
                    foreach (var layer in controller.GetUnmanagedLayers()) {
                        MakeWriteDefaultsOff(layer, defaultClip, manager.GetClipStorage().GetNoopClip(), avatarObject, missingStates);
                    }
                }
            }
            if (missingStates.Count > 0) {
                Debug.LogWarning(missingStates.Count + " properties are animated, but do not exist on the avatar:\n\n" + string.Join("\n", missingStates));
            }
        }

        private static void MakeWriteDefaultsOff(AnimatorControllerLayer layer, AnimationClip defaultClip, AnimationClip noopClip, GameObject baseObject, List<string> missingStates) {
            var alreadySet = new HashSet<EditorCurveBinding>();
            foreach (var b in AnimationUtility.GetCurveBindings(defaultClip)) alreadySet.Add(b);
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(defaultClip)) alreadySet.Add(b);

            AnimatorIterator.ForEachBlendTree(layer, tree => {
                if (tree.blendType == BlendTreeType.Direct) {
                    throw new VRCFBuilderException(
                        "You've requested VRCFury to use Write Defaults Off, but this avatar contains a Direct BlendTree in layer " + layer.name + "." +
                        " Due to a Unity bug, Write Default Off and Direct BlendTrees are incompatible.");
                }
            });

            AnimatorIterator.ForEachState(layer, state => {
                if (state.motion == null) {
                    state.motion = noopClip;
                    state.writeDefaultValues = false;
                    return;
                }

                if (!state.writeDefaultValues) {
                    return;
                }

                state.writeDefaultValues = false;
                AnimatorIterator.ForEachClip(state, clip => {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                        if (alreadySet.Contains(binding)) continue;
                        alreadySet.Add(binding);
                        var exists = AnimationUtility.GetFloatValue(baseObject, binding, out var value);
                        if (exists) {
                            AnimationUtility.SetEditorCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                        } else if (binding.path != "_ignored") {
                            missingStates.Add(
                                $"{binding.path}:{binding.type.Name}:{binding.propertyName} in {clip.name} on layer {layer.name}");
                        }
                    }

                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                        if (alreadySet.Contains(binding)) continue;
                        alreadySet.Add(binding);
                        var exists = AnimationUtility.GetObjectReferenceValue(baseObject, binding, out var value);
                        if (exists) {
                            AnimationUtility.SetObjectReferenceCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                        } else if (binding.path != "_ignored") {
                            missingStates.Add(
                                $"{binding.path}:{binding.type.Name}:{binding.propertyName} in {clip.name} on layer {layer.name}");
                        }
                    }
                });
            });
        }
        
        private bool DetectExistingWriteDefaults() {
            var offStates = 0;
            var onStates = 0;

            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetUnmanagedLayers()) {
                    AnimatorIterator.ForEachState(layer, state => {
                        if (state.writeDefaultValues) onStates++;
                        else offStates++;
                    });
                }
            }

            if (onStates > 0 && offStates > 0) {
                var weirdStates = new List<string>();
                var weirdAreOn = offStates > onStates;
                foreach (var controller in manager.GetAllUsedControllers()) {
                    foreach (var layer in controller.GetUnmanagedLayers()) {
                        AnimatorIterator.ForEachState(layer, state => {
                            if (state.writeDefaultValues == weirdAreOn) {
                                weirdStates.Add(layer.name + "." + state.name);
                            }
                        });
                    }
                }

                Debug.LogWarning("Your animation controller contains a mix of Write Defaults ON and Write Defaults OFF states." +
                                 " (" + onStates + " on, " + offStates + " off)." +
                                 " Doing this may cause weird issues to happen with your animations in game." +
                                 " This is not an issue with VRCFury, but an issue with your avatar's custom animation controller.");
                Debug.LogWarning("The broken states are most likely: " + String.Join(",", weirdStates));
            }
        
            // If half of the old states use writeDefaults, safe to assume it should be used everywhere
            return onStates >= offStates && onStates > 0;
        }
    }
}
