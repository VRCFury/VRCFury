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
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder {
        public HashSet<EditorCurveBinding> forceRecordBindings = new HashSet<EditorCurveBinding>();

        [FeatureBuilderAction(FeatureOrder.FixWriteDefaults)]
        public void Apply() {
            var analysis = DetectExistingWriteDefaults();
            var (broken, shouldBeOnIfWeAreInControl, shouldBeOnIfWeAreNotInControl, debugInfo, badStates) = analysis;

            var fixSetting = allFeaturesInRun.OfType<FixWriteDefaults>().FirstOrDefault();
            var mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;
            if (fixSetting != null) {
                mode = fixSetting.mode;
            } else if (broken) {
                var ask = EditorUtility.DisplayDialogComplex("VRCFury",
                    "VRCFury has detected a (likely) broken mix of Write Defaults on your avatar base." +
                    " This may cause weird issues to happen with your animations," +
                    " such as toggles or animations sticking on or off forever.\n\n" +
                    "VRCFury can try to fix this for you automatically. Should it try?\n\n" +
                    $"(Debug info: {debugInfo}, VRCF will try to convert to {(shouldBeOnIfWeAreInControl ? "ON" : "OFF")})",
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
                      + $" counts ({debugInfo})"
                      + $" mode ({mode})"
                      + (badStates.Count > 0 ? ("\n\nWeird states: " + string.Join(",", badStates)) : "")
            );
            
            ApplyToAvatar(applyToUnmanagedLayers, useWriteDefaults);
        }
        
        private void ApplyToAvatar(bool applyToUnmanagedLayers, bool useWriteDefaults) {
            var missingStates = new List<string>();
            foreach (var controller in applyToUnmanagedLayers ? manager.GetAllUsedControllers() : manager.GetAllTouchedControllers()) {
                var noopClip = controller.GetNoopClip();
                AnimationClip defaultClip = null;
                if (controller.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) {
                    defaultClip = controller.NewClip("Defaults " + controller.GetType());
                    var defaultLayer = controller.NewLayer("Defaults", 1);
                    defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
                }

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
            // Record default values for things
            if (defaultClip) {
                var alreadySet = new HashSet<EditorCurveBinding>();
                foreach (var b in AnimationUtility.GetCurveBindings(defaultClip)) alreadySet.Add(b);
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(defaultClip)) alreadySet.Add(b);
                AnimatorIterator.ForEachClip(layer, clip => {
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
                            AnimationUtility.SetObjectReferenceCurve(defaultClip, binding,
                                ClipBuilder.OneFrame(value));
                        } else if (!binding.path.Contains("_ignored")) {
                            missingStates.Add(
                                $"{binding.path}:{binding.type.Name}:{binding.propertyName} in {clip.name} on layer {layer.name}");
                        }
                    }
                });
            }

            // Direct blend trees break with wd off 100% of the time, so they are a rare case where the layer
            // absolutely must use wd on.
            AnimatorIterator.ForEachBlendTree(layer, tree => {
                if (tree.blendType == BlendTreeType.Direct) {
                    useWriteDefaults = true;
                }
            });

            AnimatorIterator.ForEachState(layer, state => {
                if (useWriteDefaults) { 
                    state.writeDefaultValues = true;
                } else {
                    if (state.motion == null) state.motion = noopClip;
                    if (!state.writeDefaultValues) return;
                    state.writeDefaultValues = false;
                }
            });
        }
        
        private class ControllerInfo {
            public VRCAvatarDescriptor.AnimLayerType type;
            public List<string> onStates = new List<string>();
            public List<string> offStates = new List<string>();
            public List<string> directBlendTrees = new List<string>();
            public List<string> additiveLayers = new List<string>();
        }
        
        // Returns: Broken, Should Use Write Defaults, Reason, Bad States
        private Tuple<bool, bool, bool, string, IList<string>> DetectExistingWriteDefaults() {

            var allManagedStateMachines = manager.GetAllTouchedControllers()
                .SelectMany(controller => controller.GetManagedLayers())
                .ToImmutableHashSet();

            var controllerInfos = manager.GetAllUsedControllersRaw().Select(tuple => {
                var (type, controller) = tuple;
                var info = new ControllerInfo();
                info.type = type;
                foreach (var layer in controller.layers) {
                    var isManaged = allManagedStateMachines.Contains(layer.stateMachine);
                    if (!isManaged) {
                        AnimatorIterator.ForEachState(layer.stateMachine,
                            state => {
                                (state.writeDefaultValues ? info.onStates : info.offStates).Add(layer.name + "." + state.name);
                            });
                    }

                    AnimatorIterator.ForEachBlendTree(layer.stateMachine, tree => {
                        if (tree.blendType == BlendTreeType.Direct) {
                            info.directBlendTrees.Add(tree.name);
                        }
                    });
                    if (layer.blendingMode == AnimatorLayerBlendingMode.Additive) {
                        info.additiveLayers.Add(layer.name);
                    }
                }

                return info;
            }).ToList();
            
            var debugList = new List<string>();
            foreach (var info in controllerInfos) {
                var entries = new List<string>();
                if (info.onStates.Count > 0) entries.Add(info.onStates.Count + " on");
                if (info.offStates.Count > 0) entries.Add(info.offStates.Count + " off");
                if (info.directBlendTrees.Count > 0) entries.Add(info.directBlendTrees.Count + " direct");
                if (info.additiveLayers.Count > 0) entries.Add(info.additiveLayers.Count + " additive");
                if (entries.Count > 0) {
                    debugList.Add($"{info.type}:{string.Join("|",entries)}");
                }
            }
            var debugInfo = string.Join(", ", debugList);

            IList<string> Collect(Func<ControllerInfo, IEnumerable<string>> fn) {
                return controllerInfos.SelectMany(info => fn(info).Select(s => $"{info.type} {s}")).ToList();
            }
            var onStates = Collect(info => info.onStates);
            var offStates = Collect(info => info.offStates);
            var directBlendTrees = Collect(info => info.directBlendTrees);
            var additiveLayers = Collect(info => info.additiveLayers);

            var fxInfo = controllerInfos.Find(i => i.type == VRCAvatarDescriptor.AnimLayerType.FX);
            bool shouldBeOnIfWeAreNotInControl;
            if (fxInfo != null && fxInfo.onStates.Count + fxInfo.offStates.Count > 10) {
                shouldBeOnIfWeAreNotInControl = fxInfo.onStates.Count > fxInfo.offStates.Count;
            } else {
                shouldBeOnIfWeAreNotInControl = onStates.Count > offStates.Count;
            }

            var shouldBeOnIfWeAreInControl =
                directBlendTrees.Count > 0 ||
                shouldBeOnIfWeAreNotInControl;
            
            var weirdStates = shouldBeOnIfWeAreInControl ? offStates : onStates;
            var broken = weirdStates.Count > 0;
            
            return Tuple.Create(broken, shouldBeOnIfWeAreInControl, shouldBeOnIfWeAreNotInControl, debugInfo, weirdStates);
        }
    }
}
