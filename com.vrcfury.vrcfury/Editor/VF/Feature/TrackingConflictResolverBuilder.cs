using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * Merges TrackingControl belonging to multiple owners
     */
    [VFService]
    public class TrackingConflictResolverBuilder {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly DirectBlendTreeService directTreeService;
        [VFAutowired] private readonly MathService mathService;
        
        [FeatureBuilderAction(FeatureOrder.TrackingConflictResolver)]
        public void Apply() {

            var trackingParamsCache = new Dictionary<(string, TrackingControlType), VFAFloat>();
            var trackingParams = new VFMultimap<TrackingControlType, VFAFloat>();
            var fx = manager.GetFx();

            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetLayers()) {
                    var layerOwner = controller.GetLayerOwner(layer);
                    AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                        if (b is VRCAnimatorTrackingControl trackingControl) {
                            var driver = (VRCAvatarParameterDriver)add(typeof(VRCAvatarParameterDriver));
                            foreach (var type in allTypes) {
                                var value = type.GetValue(trackingControl);
                                if (value != VRC_AnimatorTrackingControl.TrackingType.NoChange) {
                                    if (!trackingParamsCache.TryGetValue((layerOwner, type), out var param)) {
                                        param = fx.NewFloat("TC_" + layerOwner + "_" + type.fieldName);
                                        trackingParamsCache[(layerOwner, type)] = param;
                                        trackingParams.Put(type, param);
                                    }
                                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                                        name = param.Name(),
                                        value = value == VRC_AnimatorTrackingControl.TrackingType.Animation ? 1 : 0
                                    });
                                }
                            }
                            return false;
                        }

                        return true;
                    });
                }
            }

            var typesUsed = trackingParams.GetKeys().ToArray();
            if (typesUsed.Length > 0) {
                var whenAnimatedDict = new Dictionary<TrackingControlType, VFCondition>();
                {
                    foreach (var type in typesUsed) {
                        var merged = fx.NewFloat("TC_merged_" + type.fieldName);
                        var setter = mathService.MakeSetter(merged, 1);
                        foreach (var input in trackingParams.Get(type)) {
                            directTreeService.Add(input, setter);
                        }
                        whenAnimatedDict[type] = merged.IsGreaterThan(0.5f);
                    }
                }

                var layer = fx.NewLayer("VRCF Tracking Control Actor");
                var idle = layer.NewState("Idle");

                var currentSettingDict = typesUsed.ToDictionary(type => type,
                    type => fx.NewBool("TC_current_" + type.fieldName));

                void AddStates(string name, VRC_AnimatorTrackingControl.TrackingType controlValue, int driveValue, Func<VFCondition, VFCondition> mutator) {
                    var all = layer.NewState($"All - {name}");
                    all.TransitionsFromEntry().When(VFCondition.All(whenAnimatedDict.Select(e => mutator(e.Value))));
                    all.TransitionsToExit().When(fx.Always());
                    var allControl = all.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                    var allDriver = all.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    
                    foreach (var type in typesUsed) {
                        var whenAnimated = whenAnimatedDict[type];
                        var current = currentSettingDict[type];
                        var activateWhen = mutator(whenAnimated).And(mutator(current.IsFalse()));
                        var state = layer.NewState(type.fieldName + " - " + name);
                        idle.TransitionsToExit().When(activateWhen);
                        state.TransitionsFromEntry().When(activateWhen);
                        state.TransitionsToExit().When(fx.Always());
                        var control = state.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                        var driver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                        type.SetValue(allControl, controlValue);
                        type.SetValue(control, controlValue);
                        allDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = current.Name(), value = driveValue });
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = current.Name(), value = driveValue });
                    }
                }

                AddStates("Tracking", VRC_AnimatorTrackingControl.TrackingType.Tracking, 0, c => c.Not());
                AddStates("Animated", VRC_AnimatorTrackingControl.TrackingType.Animation, 1, c => c);
            }
        }

        public class TrackingControlType {
            public string fieldName;

            public VRC_AnimatorTrackingControl.TrackingType GetValue(VRC_AnimatorTrackingControl c) {
                var field = c.GetType().GetField(fieldName);
                if (field == null) return VRC_AnimatorTrackingControl.TrackingType.NoChange;
                return (VRC_AnimatorTrackingControl.TrackingType)field.GetValue(c);
            }
            
            public void SetValue(VRC_AnimatorTrackingControl c, VRC_AnimatorTrackingControl.TrackingType value) {
                var field = c.GetType().GetField(fieldName);
                field.SetValue(c, value);
            }
        }

        public static TrackingControlType[] allTypes = new [] {
            new TrackingControlType { fieldName = "trackingHead" },
            new TrackingControlType { fieldName = "trackingLeftHand" },
            new TrackingControlType { fieldName = "trackingRightHand" },
            new TrackingControlType { fieldName = "trackingHip" },
            new TrackingControlType { fieldName = "trackingLeftFoot" },
            new TrackingControlType { fieldName = "trackingRightFoot" },
            new TrackingControlType { fieldName = "trackingLeftFingers" },
            new TrackingControlType { fieldName = "trackingRightFingers" },
            new TrackingControlType { fieldName = "trackingEyes" },
            new TrackingControlType { fieldName = "trackingMouth" }
        };
    }
}