using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.PlayMode;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class FixWriteDefaultsService {

        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly OriginalAvatarService originalAvatar;
        [VFAutowired] private readonly AvatarBindingStateService bindingStateService;
        [VFAutowired] private readonly FullBodyEmoteService fullBodyEmoteService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        public void RecordDefaultNow(EditorCurveBinding binding, bool isFloat, bool force = false) {
            if (binding.type == typeof(Animator)) return;
            if (GetDefaultClip().GetCurve(binding, isFloat) != null) return;
            if (!bindingStateService.Get(binding, isFloat, out var value)) return;
            
            var shouldRecord = force;
            if (!shouldRecord) {
                // We must avoid recording our own defaults when WD is on, because a unity bug with WD on can cause our
                // defaults to override lower layers even though the lower layers should be higher priority.
                var settings = GetBuildSettings();
                shouldRecord = !settings.useWriteDefaults;
            }
            if (!shouldRecord) {
                // If our calculated default value doesn't match unity's default value, we record the default even if we're using WD on
                // because if we don't, unity will use its own default value which will be wrong.
                var found = bindingStateService.Get(binding, isFloat, out var valueDeterminedByUnity, true);
                shouldRecord = !found || value != valueDeterminedByUnity;
            }

            if (shouldRecord) {
                GetDefaultClip().SetCurve(binding, value);
            }
        }

        private VFLayer _defaultLayer = null;
        private AnimationClip _defaultClip = null;
        public AnimationClip GetDefaultClip() {
            if (_defaultClip == null) {
                _defaultClip = clipFactory.NewClip("Defaults");
                _defaultLayer = fx.NewLayer("Defaults", 0);
                _defaultLayer.NewState("Defaults").WithAnimation(_defaultClip);
            }
            if (_defaultLayer == null) {
                throw new Exception("Defaults layer disappeared during the build. Please report this on the discord.");
            }
            return _defaultClip;
        }

        [CanBeNull] public VFLayer GetDefaultLayer() {
            return _defaultLayer;
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
            foreach (var c in controllers.GetAllUsedControllers()) {
                if (c.GetType() == VRCAvatarDescriptor.AnimLayerType.FX) continue;
                foreach (var layer in c.GetLayers()) {
                    // FullBodyEmoteService anims may have non-muscle properties, but they are ALWAYS
                    // also present in the FX triggering layer, meaning they are safe to record defaults for, because any
                    // time the full body anim is on, the fx clip will also be on.
                    if (fullBodyEmoteService.DidAddLayer(layer)) continue;
                    foreach (var clip in new AnimatorIterator.Clips().From(layer)) {
                        foreach (var binding in clip.GetAllBindings()) {
                            propsInNonFx.Add(binding.Normalize());
                        }
                    }
                }
            }

            foreach (var layer in GetMaintainedLayers(fx)) {
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

            foreach (var controller in controllers.GetAllUsedControllers()) {
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

        public bool IsStillBroken() {
            return GetBuildSettings().ignoredBroken;
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
            
            var allManagedStateMachines = controllers.GetAllUsedControllers()
                .SelectMany(controller => controller.GetManagedLayers())
                .Select(l => l.stateMachine)
                .ToImmutableHashSet();

            var analysis = DetectExistingWriteDefaults(
                controllers.GetAllUsedControllers(),
                allManagedStateMachines
            );

            var fixSetting = globals.allFeaturesInRun.OfType<FixWriteDefaults>().FirstOrDefault();
            var mode = FixWriteDefaults.FixWriteDefaultsMode.Disabled;

            if (globals.allFeaturesInRun.OfType<MmdCompatibility>().Any()) {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOn;
            } else if (fixSetting != null) {
                mode = fixSetting.mode;
            } else if (analysis.isBroken) {
                var ask = DialogUtils.DisplayDialogComplex("VRCFury",
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
                    FixWriteDefaultsLater.Save(originalAvatar.GetOriginal() ?? avatarObject, ask == 0);
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
                      + (analysis.weirdStates.Count > 0 ? ("\n\nWeird states: " + analysis.weirdStates.Join(',')) : "")
            );

            _buildSettings = new BuildSettings {
                applyToUnmanagedLayers = applyToUnmanagedLayers,
                useWriteDefaults = useWriteDefaults,
                ignoredBroken = analysis.isBroken && mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled
            };
            return _buildSettings;
        }

        private IList<VFLayer> GetMaintainedLayers(ControllerManager controller) {
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
        public static DetectionResults DetectExistingWriteDefaults<T>(
            ICollection<T> avatarControllers,
            ISet<AnimatorStateMachine> stateMachinesToIgnore = null
        ) where T : VFControllerWithVrcType {
            var controllerInfos = avatarControllers.Select(controller => {
                var type = controller.vrcType;
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
                    debugList.Add($"{info.type}:{entries.Join('|')}");
                }
            }
            var debugInfo = debugList.Join(", ");

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
