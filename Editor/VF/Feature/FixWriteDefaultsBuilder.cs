using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder {
        public HashSet<EditorCurveBinding> forceRecordBindings = new HashSet<EditorCurveBinding>();

        [FeatureBuilderAction(FeatureOrder.FixWriteDefaults)]
        public void Apply() {
            var analysis = DetectExistingWriteDefaults();
            var broken = analysis.Item1;
            var shouldBeOnIfWeAreInControl = analysis.Item2;
            var shouldBeOnIfWeAreNotInControl = analysis.Item3;
            var reason = analysis.Item4;
            var badStates = analysis.Item5;

            var fixSetting = allFeaturesInRun.OfType<FixWriteDefaults>().FirstOrDefault();
            var mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;
            if (fixSetting != null) {
                mode = fixSetting.mode;
            } else if (broken) {
                var ask = EditorUtility.DisplayDialogComplex("VRCFury",
                    "VRCFury has detected a (likely) broken mix of Write Defaults on your avatar base" +
                    " (" + reason + ")." +
                    " This may cause weird issues to happen with your animations," +
                    " such as toggles or animations sticking on or off forever.\n\n" +
                    "VRCFury can try to fix this for you automatically. Should it try?",
                    "Auto-Fix",
                    "Skip",
                    "Skip and stop asking");
                if (ask == 0) {
                    mode = FixWriteDefaults.FixWriteDefaultsMode.Auto;
                }
                if ((ask == 0 || ask == 2) && originalObject) {
                    var newComponent = originalObject.AddComponent<VRCFury>();
                    var newFeature = new FixWriteDefaults();
                    if (ask == 2) newFeature.mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;
                    newComponent.config.features.Add(newFeature);
                }
            }

            bool applyToUnmanagedLayers;
            bool useWriteDefaults;
            if (mode == FixWriteDefaults.FixWriteDefaultsMode.Auto) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = shouldBeOnIfWeAreInControl;
            } else if (mode == FixWriteDefaults.FixWriteDefaultsMode.ForceOff) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = false;
            } else if (mode == FixWriteDefaults.FixWriteDefaultsMode.ForceOn) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = true;
            } else {
                applyToUnmanagedLayers = false;
                useWriteDefaults = shouldBeOnIfWeAreNotInControl;
            }
            
            Debug.Log("VRCFury is fixing write defaults "
                      + (applyToUnmanagedLayers ? "(ALL layers)" : "(Only managed layers)") + " -> "
                      + (useWriteDefaults ? "ON" : "OFF")
                      + " because (" + reason + " | " + mode + ")"
                      + (badStates.Count > 0 ? ("\n\nWeird states: " + string.Join(",", badStates)) : "")
            );
            
            ApplyToAvatar(applyToUnmanagedLayers, useWriteDefaults);
        }
        
        private void ApplyToAvatar(bool applyToUnmanagedLayers, bool useWriteDefaults) {
            var missingStates = new List<string>();
            var noopClip = manager.GetClipStorage().GetNoopClip();
            foreach (var controller in applyToUnmanagedLayers ? manager.GetAllUsedControllers() : manager.GetAllTouchedControllers()) {
                var defaultClip = manager.GetClipStorage().NewClip("Defaults " + controller.GetType());
                var defaultLayer = controller.NewLayer("Defaults", 1);
                defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
                foreach (var layer in controller.GetManagedLayers()) {
                    ApplyToLayer(layer, defaultClip, noopClip, avatarObject, missingStates, useWriteDefaults);
                }
                if (applyToUnmanagedLayers) {
                    foreach (var layer in controller.GetUnmanagedLayers()) {
                        ApplyToLayer(layer, defaultClip, noopClip, avatarObject, missingStates, useWriteDefaults);
                    }
                }
            }
            if (missingStates.Count > 0) {
                Debug.LogWarning(missingStates.Count + " properties are animated, but do not exist on the avatar:\n\n" + string.Join("\n", missingStates));
            }
        }

        private void ApplyToLayer(
            AnimatorStateMachine layer,
            AnimationClip defaultClip,
            AnimationClip noopClip,
            GameObject baseObject,
            List<string> missingStates,
            bool useWriteDefaults
        ) {
            var alreadySet = new HashSet<EditorCurveBinding>();
            foreach (var b in AnimationUtility.GetCurveBindings(defaultClip)) alreadySet.Add(b);
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(defaultClip)) alreadySet.Add(b);

            AnimatorIterator.ForEachState(layer, state => {
                if (useWriteDefaults) { 
                    state.writeDefaultValues = true;
                } else {
                    if (state.motion == null) state.motion = noopClip;
                    if (!state.writeDefaultValues) return;
                    state.writeDefaultValues = false;
                }

                AnimatorIterator.ForEachClip(state, (clip, setClip) => {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                        if (binding.type == typeof(Animator)) continue;
                        if (alreadySet.Contains(binding)) continue;
                        if (useWriteDefaults && !forceRecordBindings.Contains(binding)) continue;
                        alreadySet.Add(binding);
                        var exists = AnimationUtility.GetFloatValue(baseObject, binding, out var value);
                        if (exists) {
                            AnimationUtility.SetEditorCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                        } else if (!binding.path.Contains("_ignored")) {
                            missingStates.Add(
                                $"{binding.path}:{binding.type.Name}:{binding.propertyName} in {clip.name} on layer {layer.name}");
                        }
                    }

                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                        if (alreadySet.Contains(binding)) continue;
                        if (useWriteDefaults && !forceRecordBindings.Contains(binding)) continue;
                        alreadySet.Add(binding);
                        var exists = AnimationUtility.GetObjectReferenceValue(baseObject, binding, out var value);
                        if (exists) {
                            AnimationUtility.SetObjectReferenceCurve(defaultClip, binding, ClipBuilder.OneFrame(value));
                        } else if (!binding.path.Contains("_ignored")) {
                            missingStates.Add(
                                $"{binding.path}:{binding.type.Name}:{binding.propertyName} in {clip.name} on layer {layer.name}");
                        }
                    }
                });
            });
        }
        
        // Returns: Broken, Should Use Write Defaults, Reason, Bad States
        private Tuple<bool, bool, bool, string, List<string>> DetectExistingWriteDefaults() {
            var onStates = new List<string>();
            var offStates = new List<string>();
            var directBlendTrees = 0;
            var additiveLayers = 0;

            var allLayers = manager.GetAllUsedControllersRaw()
                .Select(c => c.Item2)
                .SelectMany(controller => controller.layers);
            var allManagedStateMachines = manager.GetAllTouchedControllers()
                .SelectMany(controller => controller.GetManagedLayers())
                .ToImmutableHashSet();

            foreach (var layer in allLayers) {
                var isManaged = allManagedStateMachines.Contains(layer.stateMachine);
                if (!isManaged) {
                    AnimatorIterator.ForEachState(layer.stateMachine, state => {
                        (state.writeDefaultValues ? onStates : offStates).Add(layer.name + "." + state.name);
                    });
                }
                AnimatorIterator.ForEachBlendTree(layer.stateMachine, tree => {
                    if (tree.blendType == BlendTreeType.Direct) {
                        directBlendTrees++;
                    }
                });
                if (layer.blendingMode == AnimatorLayerBlendingMode.Additive) {
                    additiveLayers++;
                }
            }

            var shouldBeOnIfWeAreInControl =
                directBlendTrees > 0 ||
                //additiveLayers > 0 ||
                onStates.Count > offStates.Count;
            var shouldBeOnIfWeAreNotInControl = onStates.Count > offStates.Count;
            var weirdStates = shouldBeOnIfWeAreInControl ? offStates : onStates;
            var outList = new List<string>();
            if (onStates.Count > 0) outList.Add(onStates.Count + " on");
            if (offStates.Count > 0) outList.Add(offStates.Count + " off");
            if (directBlendTrees > 0) outList.Add(directBlendTrees + " direct");
            if (additiveLayers > 0) outList.Add(additiveLayers + " additive");

            var broken = weirdStates.Count > 0;
            var reason = string.Join(", ", outList);
            return Tuple.Create(broken, shouldBeOnIfWeAreInControl, shouldBeOnIfWeAreNotInControl, reason, weirdStates);
        }
    }
}
