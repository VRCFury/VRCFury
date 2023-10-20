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
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class FixWriteDefaultsBuilder : FeatureBuilder {

        public void RecordDefaultNow(EditorCurveBinding binding, bool isFloat = true) {
            if (binding.type == typeof(Animator)) return;

            if (isFloat) {
                if (GetDefaultClip().GetFloatCurve(binding) != null) return;
                if (binding.GetFloatFromGameObject(avatarObject, out var value)) {
                    GetDefaultClip().SetConstant(binding, value);
                }
            } else {
                if (GetDefaultClip().GetObjectCurve(binding) != null) return;
                if (binding.GetObjectFromGameObject(avatarObject, out var value)) {
                    GetDefaultClip().SetConstant(binding, value);
                }
            }
        }
        
        private AnimationClip _defaultClip = null;
        private AnimationClip GetDefaultClip() {
            if (_defaultClip == null) {
                var fx = GetFx();
                _defaultClip = fx.NewClip("Defaults");
                var defaultLayer = fx.NewLayer("Defaults", 0);
                defaultLayer.NewState("Defaults").WithAnimation(_defaultClip);
            }
            return _defaultClip;
        }

        [FeatureBuilderAction(FeatureOrder.RecordAllDefaults)]
        public void RecordAllDefaults() {
            // We shouldn't need to record defaults if useWriteDefaults is true, BUT due to a vrchat bug,
            // the defaults state for properties are broken in mirrors, so we're forced to record them all in the base layer.
            //var settings = GetBuildSettings();
            //if (settings.useWriteDefaults) return;

            foreach (var layer in GetMaintainedLayers(GetFx())) {
                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    if (!state.writeDefaultValues) continue;
                    foreach (var clip in new AnimatorIterator.Clips().From(state)) {
                        foreach (var binding in clip.GetFloatBindings()) {
                            RecordDefaultNow(binding, true);
                        }
                        foreach (var binding in clip.GetObjectBindings()) {
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

                    foreach (var state in new AnimatorIterator.States().From(layer)) {
                        state.writeDefaultValues = useWriteDefaultsForLayer;
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

            var analysis = DetectExistingWriteDefaults(manager.GetAllUsedControllersRaw(), allManagedStateMachines);

            var fixSetting = allFeaturesInRun.OfType<FixWriteDefaults>().FirstOrDefault();
            var mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;
            if (fixSetting != null) {
                mode = fixSetting.mode;
            } else if (analysis.isBroken) {
                var ask = EditorUtility.DisplayDialogComplex("VRCFury",
                    "VRCFury has detected a (likely) broken mix of Write Defaults on your avatar base." +
                    " This may cause weird issues to happen with your animations," +
                    " such as toggles or animations sticking on or off forever.\n\n" +
                    "VRCFury can try to fix this for you automatically. Should it try?\n\n" +
                    $"(Debug info: {analysis.debugInfo}, VRCF will try to convert to {(analysis.shouldBeOnIfWeAreInControl ? "ON" : "OFF")})",
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
            public List<string> additiveLayers = new List<string>();
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
            IEnumerable<Tuple<VRCAvatarDescriptor.AnimLayerType, VFController>> avatarControllers,
            ISet<AnimatorStateMachine> stateMachinesToIgnore = null
        ) {
            var controllerInfos = avatarControllers.Select(tuple => {
                var (type, controller) = tuple;
                var info = new ControllerInfo();
                info.type = type;
                foreach (var layer in controller.layers) {
                    var ignore = stateMachinesToIgnore != null && stateMachinesToIgnore.Contains(layer.stateMachine);
                    if (!ignore) {
                        foreach (var state in new AnimatorIterator.States().From(layer)) {
                            var hasDirect = new AnimatorIterator.Trees().From(state)
                                .Any(tree => tree.blendType == BlendTreeType.Direct);

                            var list = hasDirect
                                ? (state.writeDefaultValues ? info.directOnStates : info.directOffStates)
                                : (state.writeDefaultValues ? info.onStates : info.offStates);
                            list.Add(layer.name + " | " + state.name);
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
                return controllerInfos.SelectMany(info => fn(info).Select(s => $"{info.type} | {s}")).ToList();
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
