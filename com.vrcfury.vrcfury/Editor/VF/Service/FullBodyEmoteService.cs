using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    public class FullBodyEmoteService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;
        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder animatorLayerControlManager;
        
        private readonly Dictionary<EditorCurveBindingExtensions.MuscleBindingType, Func<AnimationClip,VFAFloat>> addCache
            = new Dictionary<EditorCurveBindingExtensions.MuscleBindingType, Func<AnimationClip, VFAFloat>>();

        public VFAFloat AddClip(AnimationClip clip, EditorCurveBindingExtensions.MuscleBindingType type) {
            if (!addCache.ContainsKey(type)) {
                if (type == EditorCurveBindingExtensions.MuscleBindingType.Other) {
                    var action = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Action);
                    var layer = action.NewLayer("VRCFury Actions");
                    layer.mask = AvatarMaskExtensions.Empty();
                    layer.mask.AllowAllMuscles();
                    var idle = layer.NewState("Idle");
                    addCache[type] = c => AddClip(c, action, idle, layer, type);
                } else {
                    var fx = manager.GetController(VRCAvatarDescriptor.AnimLayerType.FX);
                    var isLeft = type == EditorCurveBindingExtensions.MuscleBindingType.LeftHand;
                    var layer = fx.NewLayer(
                        "VRCFury " +
                        (isLeft
                            ? "Left Hand"
                            : "Right Hand")
                    );
                    layer.weight = 0;
                    layer.mask = AvatarMaskExtensions.Empty();
                    layer.mask.SetHumanoidBodyPartActive(isLeft ? AvatarMaskBodyPart.LeftFingers : AvatarMaskBodyPart.RightFingers, true);
                    var idle = layer.NewState("Idle");
                    addCache[type] = c => AddClip(c, fx, idle, layer, type);
                }
            }

            return addCache[type](clip);
        }
        
        private VFAFloat AddClip(AnimationClip clip, ControllerManager ctrl, VFState idle, VFLayer layer, EditorCurveBindingExtensions.MuscleBindingType type) {
            clip = MutableManager.CopyRecursive(clip, false);
            var nonActionBindings = clip.GetAllBindings()
                .Where(b => b.GetMuscleBindingType() != type);
            clip.SetCurves(nonActionBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            var state = layer.NewState(clip.name).WithAnimation(clip);

            var fx = manager.GetFx();
            var enableParam = fx.NewFloat(clip.name + " (Trigger)");
            VFCondition myCond;
            if (ctrl == manager.GetFx()) {
                myCond = enableParam.IsGreaterThan(0);
            } else {
                var myParam = ctrl.NewBool(clip.name+" (Action)");
                driveOtherTypesFromFloatService.Drive(enableParam, myParam.Name(), 1);
                myCond = myParam.IsTrue();
            }
            state.TransitionsFromEntry().When(myCond);
            idle.TransitionsToExit().When(myCond);

            var outState = layer.NewState($"{clip.name} - Out");
            state.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(myCond.Not());
            outState.TransitionsToExit().When(ctrl.Always());

            if (type == EditorCurveBindingExtensions.MuscleBindingType.Other) {
                var weightOn = state.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                weightOn.goalWeight = 1;
                var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                weightOff.goalWeight = 0;
            } else {
                var weightOn = state.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                animatorLayerControlManager.Register(weightOn, layer);
                weightOn.goalWeight = 1;
                var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                animatorLayerControlManager.Register(weightOff, layer);
                weightOff.goalWeight = 0;
            }

            var animOn = state.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            var animOff = outState.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            foreach (var trackingType in TrackingConflictResolverBuilder.allTypes) {
                if (type == EditorCurveBindingExtensions.MuscleBindingType.LeftHand) {
                    if (trackingType != TrackingConflictResolverBuilder.TrackingLeftFingers) {
                        continue;
                    }
                } else if (type == EditorCurveBindingExtensions.MuscleBindingType.RightHand) {
                    if (trackingType != TrackingConflictResolverBuilder.TrackingRightFingers) {
                        continue;
                    }
                } else {
                    if (trackingType == TrackingConflictResolverBuilder.TrackingEyes
                        || trackingType == TrackingConflictResolverBuilder.TrackingMouth
                        || trackingType == TrackingConflictResolverBuilder.TrackingLeftFingers
                        || trackingType == TrackingConflictResolverBuilder.TrackingRightFingers
                    ) {
                        continue;
                    }
                }
                trackingType.SetValue(animOn, VRC_AnimatorTrackingControl.TrackingType.Animation);
                trackingType.SetValue(animOff, VRC_AnimatorTrackingControl.TrackingType.Tracking);
            }
            
            return enableParam;
        }
    }
}
