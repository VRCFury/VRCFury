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
using VF.PlayMode;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder {

        public void RecordDefaultNow(EditorCurveBinding binding, bool isFloat = true) {
            if (binding.type == typeof(Animator)) return;

            if (isFloat) {
                if (GetDefaultClip().GetFloatCurve(binding) != null) return;
                if (binding.GetFloatFromGameObject(avatarObject, out var value)) {
                    GetDefaultClip().SetCurve(binding, value);
                }
            } else {
                if (GetDefaultClip().GetObjectCurve(binding) != null) return;
                if (binding.GetObjectFromGameObject(avatarObject, out var value)) {
                    GetDefaultClip().SetCurve(binding, value);
                }
            }
        }

        private VFLayer _defaultLayer = null;
        private AnimationClip _defaultClip = null;
        private AnimationClip GetDefaultClip() {
            if (_defaultClip == null) {
                var fx = GetFx();
                _defaultClip = fx.NewClip("Defaults");
                _defaultLayer = fx.NewLayer("Defaults", 0);
                _defaultLayer.NewState("Defaults").WithAnimation(_defaultClip);
            }
            return _defaultClip;
        }

        [FeatureBuilderAction(FeatureOrder.PositionDefaultsLayer)]
        public void PositionDefaultsLayer() {
            if (_defaultLayer != null) {
                _defaultLayer.Move(0);
            }
        }

        [FeatureBuilderAction(FeatureOrder.RecordAllDefaults)]
        public void RecordAllDefaults() {
            var propsInNonFx = new HashSet<EditorCurveBinding>();
            foreach (var c in manager.GetAllUsedControllers()) {
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) continue;
                foreach (var clip in c.GetClips()) {
                    foreach (var binding in clip.GetAllBindings()) {
                        propsInNonFx.Add(binding.Normalize());
                    }
                }
            }
            
            // Note to self: Never record defaults when WD is on, because a unity bug with WD on can cause the defaults to override lower layers
            // even though the lower layers should be higher priority.
            var settings = GetBuildSettings();
            if (settings.useWriteDefaults) return;

            foreach (var layer in GetMaintainedLayers(GetFx())) {
                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    if (!state.writeDefaultValues) continue;
                    foreach (var clip in new AnimatorIterator.Clips().From(state)) {
                        foreach (var binding in clip.GetFloatBindings()) {
                            if (propsInNonFx.Contains(binding.Normalize())) continue;
                            RecordDefaultNow(binding, true);
                        }
                        foreach (var binding in clip.GetObjectBindings()) {
                            if (propsInNonFx.Contains(binding.Normalize())) continue;
                            RecordDefaultNow(binding, false);
                        }
                    }
                }
            }
        }

        [FeatureBuilderAction(FeatureOrder.AdjustWriteDefaults)]
        public void AdjustWriteDefaults() {
            var settings = GetBuildSettings();

            if (settings.ignoredBroken) {
                var fx = manager.GetFx();
                fx.NewBool($"VF/BrokenWd", usePrefix: false, synced: true, networkSynced: false);
            }

            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in GetMaintainedLayers(controller)) {
                    // Direct blend trees break with wd off 100% of the time, so they are a rare case where the layer
                    // absolutely must use wd on.
                    var useWriteDefaultsForLayer = settings.useWriteDefaults;
                    useWriteDefaultsForLayer |= new AnimatorIterator.Trees().From(layer)
                        .Any(tree => tree.blendType == BlendTreeType.Direct);
                    useWriteDefaultsForLayer |= layer.blendingMode == AnimatorLayerBlendingMode.Additive
                        || controller.GetType() == VRCAvatarDescriptor.AnimLayerType.Additive;

                    foreach (var state in new AnimatorIterator.States().From(layer)) {
                        // Avoid calling this if not needed, since it internally invalidates the controller cache every time
                        if (state.writeDefaultValues != useWriteDefaultsForLayer) {
                            state.writeDefaultValues = useWriteDefaultsForLayer;
                        }
                    }
                }
            }
        }

        private class BuildSettings {
            public bool applyToUnmanagedLayers;
            public bool useWriteDefaults;
            public bool ignoredBroken;
        }
        private BuildSettings _buildSettings;
        private BuildSettings GetBuildSettings() {
            if (_buildSettings != null) {
                return _buildSettings;
            }
            
            var allManagedStateMachines = manager.GetAllUsedControllers()
                .SelectMany(controller => controller.GetManagedLayers())
                .Select(l => l.stateMachine)
                .ToImmutableHashSet();

            var analysis = DetectExistingWriteDefaults(
                manager.GetAllUsedControllers().Select(c => (c.GetType(), c.GetRaw())),
                allManagedStateMachines
            );

            var fixSetting = allFeaturesInRun.OfType<FixWriteDefaults>().FirstOrDefault();
            var mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;

            if (allFeaturesInRun.OfType<MmdCompatibility>().Any()) {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOn;
            } else if (fixSetting != null) {
                mode = fixSetting.mode;
            } else if (analysis.isBroken) {
                var ask = EditorUtility.DisplayDialogComplex("VRCFury",
                    "VRCFury has detected a (likely) broken mix of Write Defaults on your avatar base." +
                    " This may cause weird issues to happen with your animations," +
                    " such as toggles or animations sticking on or off forever.\n\n" +
                    "VRCFury can try to fix this for you automatically. Should it try?\n\n" +
                    "You can easily undo this change by removing the 'Fix Write Defaults' component that will be added to your avatar root.\n\n" +
                    $"(Debug info: {analysis.debugInfo}, VRCF will try to convert to {(analysis.shouldBeOnIfWeAreInControl ? "ON" : "OFF")})",
                    "Auto-Fix",
                    "Skip",
                    "Skip and stop asking");
                if (ask == 0) {
                    mode = FixWriteDefaults.FixWriteDefaultsMode.Auto;
                }
                // Save the choice
                if (ask == 0 || ask == 2) {
                    if (Application.isPlaying) {
                        FixWriteDefaultsLater.SaveLater(avatarObject, ask == 0);
                    } else if (originalObject) {
                        FixWriteDefaultsLater.SaveNow(originalObject, ask == 0);
                    }
                }
            }

            bool applyToUnmanagedLayers;
            bool useWriteDefaults;
            if (mode == FixWriteDefaults.FixWriteDefaultsMode.Auto) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = analysis.shouldBeOnIfWeAreInControl;
            } else if (mode == FixWriteDefaults.FixWriteDefaultsMode.ForceOff) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = false;
            } else if (mode == FixWriteDefaults.FixWriteDefaultsMode.ForceOn) {
                applyToUnmanagedLayers = true;
                useWriteDefaults = true;
            } else {
                applyToUnmanagedLayers = false;
                useWriteDefaults = analysis.shouldBeOnIfWeAreNotInControl;
            }
            
            Debug.Log("VRCFury is fixing write defaults "
                      + (applyToUnmanagedLayers ? "(ALL layers)" : "(Only managed layers)") + " -> "
                      + (useWriteDefaults ? "ON" : "OFF")
                      + $" counts ({analysis.debugInfo})"
                      + $" mode ({mode})"
                      + (analysis.weirdStates.Count > 0 ? ("\n\nWeird states: " + string.Join(",", analysis.weirdStates)) : "")
            );

            _buildSettings = new BuildSettings {
                applyToUnmanagedLayers = applyToUnmanagedLayers,
                useWriteDefaults = useWriteDefaults,
                ignoredBroken = analysis.isBroken && mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled
            };
            return _buildSettings;
        }

        private IEnumerable<VFLayer> GetMaintainedLayers(ControllerManager controller) {
            var settings = GetBuildSettings();
            return settings.applyToUnmanagedLayers ? controller.GetLayers() : controller.GetManagedLayers();
        }

        private class ControllerInfo {
            public VRCAvatarDescriptor.AnimLayerType type;
            public List<string> onStates = new List<string>();
            public List<string> offStates = new List<string>();
            public List<string> directOnStates = new List<string>();
            public List<string> directOffStates = new List<string>();
            public List<string> additiveOnStates = new List<string>();
            public List<string> additiveOffStates = new List<string>();
        }

        public class DetectionResults {
            public bool isBroken;
            public bool shouldBeOnIfWeAreInControl;
            public bool shouldBeOnIfWeAreNotInControl;
            public string debugInfo;
            public IList<string> weirdStates;
        }
        
        // Returns: Broken, Should Use Write Defaults, Reason, Bad States
        public static DetectionResults DetectExistingWriteDefaults(
            IEnumerable<(VRCAvatarDescriptor.AnimLayerType, VFController)> avatarControllers,
            ISet<AnimatorStateMachine> stateMachinesToIgnore = null
        ) {
            var controllerInfos = avatarControllers.Select(tuple => {
                var (type, controller) = tuple;
                var info = new ControllerInfo();
                info.type = type;
                foreach (var layer in controller.GetLayers()) {
                    var ignore = stateMachinesToIgnore != null && stateMachinesToIgnore.Contains(layer.stateMachine);
                    if (!ignore) {
                        foreach (var state in new AnimatorIterator.States().From(layer)) {
                            List<string> list;
                            if (layer.blendingMode == AnimatorLayerBlendingMode.Additive || type == VRCAvatarDescriptor.AnimLayerType.Additive) {
                                list = state.writeDefaultValues ? info.additiveOnStates : info.additiveOffStates;
                            } else if (new AnimatorIterator.Trees().From(state).Any(tree => tree.blendType == BlendTreeType.Direct)) {
                                list = state.writeDefaultValues ? info.directOnStates : info.directOffStates;
                            } else {
                                list = state.writeDefaultValues ? info.onStates : info.offStates;
                            }
                            list.Add(layer.name + " | " + state.name);
                        }
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
                if (info.additiveOnStates.Count > 0) entries.Add(info.additiveOnStates.Count + " additive-on");
                if (info.additiveOffStates.Count > 0) entries.Add(info.additiveOffStates.Count + " additive-off");
                if (entries.Count > 0) {
                    debugList.Add($"{info.type}:{string.Join("|",entries)}");
                }
            }
            var debugInfo = string.Join(", ", debugList);

            IList<string> Collect(Func<ControllerInfo, IEnumerable<string>> fn) {
                return controllerInfos.SelectMany(info => fn(info).Select(s => $"{info.type} | {s}")).ToList();
            }
            var onStates = Collect(info => info.onStates);
            var offStates = Collect(info => info.offStates);
            var directOffStates = Collect(info => info.directOffStates);
            var additiveOffStates = Collect(info => info.additiveOffStates);

            var fxInfo = controllerInfos.Find(i => i.type == VRCAvatarDescriptor.AnimLayerType.FX);
            bool shouldBeOnIfWeAreNotInControl;
            if (fxInfo != null && fxInfo.onStates.Count + fxInfo.offStates.Count > 10) {
                shouldBeOnIfWeAreNotInControl = fxInfo.onStates.Count > fxInfo.offStates.Count;
            } else {
                shouldBeOnIfWeAreNotInControl = onStates.Count > offStates.Count;
            }

            var shouldBeOnIfWeAreInControl = shouldBeOnIfWeAreNotInControl;
            
            var weirdStates = (shouldBeOnIfWeAreNotInControl ? offStates : onStates).Concat(directOffStates).Concat(additiveOffStates).ToList();
            var broken = weirdStates.Count > 0;

            return new DetectionResults {
                isBroken = broken,
                shouldBeOnIfWeAreInControl = shouldBeOnIfWeAreInControl,
                shouldBeOnIfWeAreNotInControl = shouldBeOnIfWeAreNotInControl,
                debugInfo = debugInfo,
                weirdStates = weirdStates
            };
        }
    }
}
