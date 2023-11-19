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

        private VFMultimap<TrackingControlType, VFAFloat> inhibitors =
            new VFMultimap<TrackingControlType, VFAFloat>();

        public VFAFloat AddInhibitor(string owner, TrackingControlType type) {
            var param = manager.GetFx().NewFloat($"TC_{owner}_{type.fieldName}");
            inhibitors.Put(type, param);
            return param;
        }

        private List<Action> whenCollected = new List<Action>();
        public void WhenCollected(Action a) {
            whenCollected.Add(a);
        }

        public IList<VFAFloat> GetInhibitors(TrackingControlType type) {
            return inhibitors.Get(type);
        }

        [FeatureBuilderAction(FeatureOrder.TrackingConflictResolver)]
        public void Apply() {

            var trackingParamsCache = new Dictionary<(string, TrackingControlType), VFAFloat>();
            var fx = manager.GetFx();

            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetLayers()) {
                    var layerOwner = controller.GetLayerOwner(layer);
                    AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                        if (!(b is VRCAnimatorTrackingControl trackingControl)) return true;
                        var driver = (VRCAvatarParameterDriver)add(typeof(VRCAvatarParameterDriver));
                        foreach (var type in allTypes) {
                            var value = type.GetValue(trackingControl);
                            if (value == VRC_AnimatorTrackingControl.TrackingType.NoChange) continue;
                            if (!trackingParamsCache.TryGetValue((layerOwner, type), out var param)) {
                                param = AddInhibitor(layerOwner, type);
                                trackingParamsCache[(layerOwner, type)] = param;
                            }
                            driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                                name = param.Name(),
                                value = value == VRC_AnimatorTrackingControl.TrackingType.Animation ? 1 : 0
                            });
                        }
                        return false;

                    });
                }
            }

            foreach (var a in whenCollected) {
                a();
            }

            var typesUsed = inhibitors.GetKeys().ToArray();
            if (typesUsed.Length > 0) {
                var whenAnimatedDict = new Dictionary<TrackingControlType, VFCondition>();
                {
                    foreach (var type in typesUsed) {
                        var merged = fx.NewFloat("TC_merged_" + type.fieldName);
                        var setter = mathService.MakeSetter(merged, 1);
                        foreach (var input in inhibitors.Get(type)) {
                            directTreeService.Add(input, setter);
                        }
                        whenAnimatedDict[type] = merged.IsGreaterThan(0.5f);
                    }
                }

                var layer = fx.NewLayer("VRCF Tracking Control Actor");
                var idle = layer.NewState("Idle");

                var currentSettingDict = typesUsed.ToDictionary(type => type,
                    type => fx.NewBool("TC_current_" + type.fieldName));

                void AddStates(
                    string modeName,
                    VRC_AnimatorTrackingControl.TrackingType controlValue,
                    int driveValue,
                    Func<VFCondition, VFCondition> mutator
                ) {
                    void AddState(
                        string stateName,
                        ICollection<TrackingControlType> types,
                        bool addTransitionFromIdle = false,
                        bool checkAlreadyActive = true
                    ) {
                        if (types.Count == 0) return;

                        var state = layer.NewState($"{stateName} - {modeName}");
                        var triggerWhen = VFCondition.All(types.Select(type => mutator(whenAnimatedDict[type])));
                        if (checkAlreadyActive) {
                            var isAlreadyActive = VFCondition.All(types.Select(type => mutator(currentSettingDict[type].IsTrue())));
                            triggerWhen = triggerWhen.And(isAlreadyActive.Not());
                        }

                        if (addTransitionFromIdle) {
                            idle.TransitionsToExit().When(triggerWhen);
                        }

                        state.TransitionsFromEntry().When(triggerWhen);
                        state.TransitionsToExit().When(checkAlreadyActive ? fx.Always() : triggerWhen.Not());

                        var control = state.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                        var driver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    
                        foreach (var type in types) {
                            type.SetValue(control, controlValue);
                            driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = currentSettingDict[type].Name(), value = driveValue });
                        }
                    }
                    
                    AddState("All", typesUsed, checkAlreadyActive: false);
                    AddState("NoFace", typesUsed.Where(t => !t.isFace).ToArray());
                    foreach (var type in typesUsed) {
                        AddState(type.fieldName, new [] { type }, addTransitionFromIdle: true);
                    }
                }

                AddStates("Tracking", VRC_AnimatorTrackingControl.TrackingType.Tracking, 0, c => c.Not());
                AddStates("Animated", VRC_AnimatorTrackingControl.TrackingType.Animation, 1, c => c);
            }
        }

        public class TrackingControlType {
            public string fieldName;
            public bool isFace;

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

        public static TrackingControlType TrackingHead = new TrackingControlType { fieldName = "trackingHead" };
        public static TrackingControlType TrackingLeftHand = new TrackingControlType { fieldName = "trackingLeftHand" };
        public static TrackingControlType TrackingRightHand = new TrackingControlType { fieldName = "trackingRightHand" };
        public static TrackingControlType TrackingHip = new TrackingControlType { fieldName = "trackingHip" };
        public static TrackingControlType TrackingLeftFoot = new TrackingControlType { fieldName = "trackingLeftFoot" };
        public static TrackingControlType TrackingRightFoot = new TrackingControlType { fieldName = "trackingRightFoot" };
        public static TrackingControlType TrackingLeftFingers = new TrackingControlType { fieldName = "trackingLeftFingers" };
        public static TrackingControlType TrackingRightFingers = new TrackingControlType { fieldName = "trackingRightFingers" };
        public static TrackingControlType TrackingEyes = new TrackingControlType { fieldName = "trackingEyes", isFace = true };
        public static TrackingControlType TrackingMouth = new TrackingControlType { fieldName = "trackingMouth", isFace = true };
        

        public static TrackingControlType[] allTypes = new [] {
            TrackingHead,
            TrackingLeftHand,
            TrackingRightHand,
            TrackingHip,
            TrackingLeftFoot,
            TrackingRightFoot,
            TrackingLeftFingers,
            TrackingRightFingers,
            TrackingEyes,
            TrackingMouth
        };
    }
}
