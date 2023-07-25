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
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder {

        public void RecordDefaultNow(EditorCurveBinding binding, bool isFloat = true) {
            if (binding.type == typeof(Animator)) return;

            if (isFloat) {
                if (GetDefaultClip().GetFloatCurve(binding) != null) return;
                var exists = AnimationUtility.GetFloatValue(avatarObject, binding, out var value);
                if (exists) {
                    GetDefaultClip().SetFloatCurve(binding, ClipBuilder.OneFrame(value));
                }
            } else {
                if (GetDefaultClip().GetObjectCurve(binding) != null) return;
                var exists = AnimationUtility.GetObjectReferenceValue(avatarObject, binding, out var value);
                if (exists) {
                    GetDefaultClip().SetObjectCurve(binding, ClipBuilder.OneFrame(value));
                }
            }
        }
        
        private AnimationClip _defaultClip = null;
        private AnimationClip GetDefaultClip() {
            if (_defaultClip == null) {
                _defaultClip = GetFx().NewClip("Defaults");
                allBuildersInRun.OfType<ObjectMoveBuilder>().First().AddAdditionalManagedClip(_defaultClip);
            }
            return _defaultClip;
        }

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
            foreach (var controller in applyToUnmanagedLayers ? manager.GetAllUsedControllers() : manager.GetAllTouchedControllers()) {
                var noopClip = controller.GetNoopClip();
                var recordDefaults = !useWriteDefaults && controller.GetType() == VRCAvatarDescriptor.AnimLayerType.FX;
                foreach (var layer in controller.GetManagedLayers()) {
                    ApplyToLayer(layer, noopClip, useWriteDefaults, recordDefaults);
                }
                if (applyToUnmanagedLayers) {
                    foreach (var layer in controller.GetUnmanagedLayers()) {
                        ApplyToLayer(layer, noopClip, useWriteDefaults, recordDefaults);
                    }
                }
            }

            var defaultClip = GetDefaultClip();
            if (defaultClip.GetFloatBindings().Length > 0 || defaultClip.GetObjectBindings().Length > 0) {
                var defaultLayer = GetFx().NewLayer("Defaults", 0);
                defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
                foreach (var state in new AnimatorIterator.States().From(defaultLayer.GetRawStateMachine())) {
                    state.writeDefaultValues = useWriteDefaults;
                }
            }
        }

        private void ApplyToLayer(
            AnimatorStateMachine layer,
            AnimationClip noopClip,
            bool useWriteDefaults,
            bool recordDefaults
        ) {
            // Record default values for things
            if (recordDefaults) {
                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    if (!state.writeDefaultValues) continue;
                    foreach (var clip in new AnimatorIterator.Clips().From(state)) {
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                            RecordDefaultNow(binding, true);
                        }
                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                            RecordDefaultNow(binding, false);
                        }
                    }
                }
            }

            // Direct blend trees break with wd off 100% of the time, so they are a rare case where the layer
            // absolutely must use wd on.
            useWriteDefaults |= new AnimatorIterator.Trees().From(layer)
                .Any(tree => tree.blendType == BlendTreeType.Direct);

            foreach (var state in new AnimatorIterator.States().From(layer)) {
                if (useWriteDefaults) { 
                    state.writeDefaultValues = true;
                } else {
                    if (state.motion == null) state.motion = noopClip;
                    if (!state.writeDefaultValues) return;
                    state.writeDefaultValues = false;
                }
            }
        }
        
        private class ControllerInfo {
            public VRCAvatarDescriptor.AnimLayerType type;
            public List<string> onStates = new List<string>();
            public List<string> offStates = new List<string>();
            public List<string> directOnStates = new List<string>();
            public List<string> directOffStates = new List<string>();
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
                        foreach (var state in new AnimatorIterator.States().From(layer)) {
                            var hasDirect = new AnimatorIterator.Trees().From(state)
                                .Any(tree => tree.blendType == BlendTreeType.Direct);

                            var list = hasDirect
                                ? (state.writeDefaultValues ? info.directOnStates : info.directOffStates)
                                : (state.writeDefaultValues ? info.onStates : info.offStates);
                            list.Add(layer.name + "." + state.name);
                        }
                    }
                    
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
                if (info.directOnStates.Count > 0) entries.Add(info.directOnStates.Count + " direct-on");
                if (info.directOffStates.Count > 0) entries.Add(info.directOffStates.Count + " direct-off");
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
            var directOffStates = Collect(info => info.directOffStates);

            var fxInfo = controllerInfos.Find(i => i.type == VRCAvatarDescriptor.AnimLayerType.FX);
            bool shouldBeOnIfWeAreNotInControl;
            if (fxInfo != null && fxInfo.onStates.Count + fxInfo.offStates.Count > 10) {
                shouldBeOnIfWeAreNotInControl = fxInfo.onStates.Count > fxInfo.offStates.Count;
            } else {
                shouldBeOnIfWeAreNotInControl = onStates.Count > offStates.Count;
            }

            var shouldBeOnIfWeAreInControl = shouldBeOnIfWeAreNotInControl;
            
            var weirdStates = (shouldBeOnIfWeAreNotInControl ? offStates : onStates).Concat(directOffStates).ToList();
            var broken = weirdStates.Count > 0;
            
            return Tuple.Create(broken, shouldBeOnIfWeAreInControl, shouldBeOnIfWeAreNotInControl, debugInfo, (IList<string>)weirdStates);
        }
    }
}
