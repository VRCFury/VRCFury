using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder<FixWriteDefaults> {
        [FeatureBuilderAction(10000)]
        public void Apply() {
            if (allFeaturesInRun.Any(f => f is MakeWriteDefaultsOff)) {
                MakeWriteDefaultsOff(true);
                return;
            }
            
            var useWriteDefaults = DetectExistingWriteDefaults(controller);
            if (!useWriteDefaults) {
                Debug.Log("Detected 'Write Defaults Off', adjusting VRCFury states to use it too.");
                MakeWriteDefaultsOff(false);
                return;
            }
        }
        
        private void MakeWriteDefaultsOff(bool applyToUnmanagedLayers) {
            var defaultClip = controller.NewClip("Defaults");
            var defaultLayer = controller.NewLayer("Defaults", true);
            defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
            var missingStates = new List<string>();
            foreach (var layer in controller.GetManagedLayers()) {
                MakeWriteDefaultsOff(layer, defaultClip, controller.GetNoopClip(), avatarObject, missingStates);
            }
            if (applyToUnmanagedLayers) {
                foreach (var layer in controller.GetUnmanagedLayers()) {
                    MakeWriteDefaultsOff(layer, defaultClip, controller.GetNoopClip(), avatarObject, missingStates);
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

            DefaultClipBuilder.ForEachState(layer, state => {
                if (state.motion == null) {
                    state.motion = noopClip;
                    state.writeDefaultValues = false;
                    return;
                }

                if (!state.writeDefaultValues) {
                    return;
                }

                state.writeDefaultValues = false;
                DefaultClipBuilder.ForEachClip(state, clip => {
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
        
        private static bool DetectExistingWriteDefaults(ControllerManager manager) {
            var offStates = 0;
            var onStates = 0;
            foreach (var layer in manager.GetUnmanagedLayers()) {
                DefaultClipBuilder.ForEachState(layer, state => {
                    if (state.writeDefaultValues) onStates++;
                    else offStates++;
                });
            }

            if (onStates > 0 && offStates > 0) {
                var weirdStates = new List<string>();
                var weirdAreOn = offStates > onStates;
                foreach (var layer in manager.GetUnmanagedLayers()) {
                    DefaultClipBuilder.ForEachState(layer, state => {
                        if (state.writeDefaultValues == weirdAreOn) {
                            weirdStates.Add(layer.name+"."+state.name);
                        }
                    });
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
