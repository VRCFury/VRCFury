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
    internal class FullBodyEmoteService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly FloatToDriverService floatToDriverService;
        [VFAutowired] private readonly AnimatorLayerControlOffsetService animatorLayerControlManager;
        
        private readonly Dictionary<EditorCurveBindingExtensions.MuscleBindingType, Func<AnimationClip,VFAFloat>> addCache
            = new Dictionary<EditorCurveBindingExtensions.MuscleBindingType, Func<AnimationClip, VFAFloat>>();

        public VFAFloat AddClip(AnimationClip clip, EditorCurveBindingExtensions.MuscleBindingType type) {
            if (!addCache.ContainsKey(type)) {
                if (type == EditorCurveBindingExtensions.MuscleBindingType.Body) {
                    var action = controllers.GetController(VRCAvatarDescriptor.AnimLayerType.Action);
                    var name = "VRCFury Actions";
                    var param = action.NewInt(name);
                    var paramIndex = 0;
                    var layer = action.NewLayer(name);
                    addedLayers.Add(layer);
                    var idle = layer.NewState("Idle");
                    addCache[type] = c => AddClip(c, action, idle, layer, type, param, ++paramIndex);
                } else {
                    var gesture = controllers.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
                    var isLeft = type == EditorCurveBindingExtensions.MuscleBindingType.LeftHand;
                    var name = "VRCFury " + (isLeft ? "Left Hand" : "Right Hand");
                    var param = gesture.NewInt(name);
                    var paramIndex = 0;
                    var layer = gesture.NewLayer(name);
                    addedLayers.Add(layer);
                    layer.weight = 0;
                    layer.mask = AvatarMaskExtensions.Empty();
                    layer.mask.SetHumanoidBodyPartActive(isLeft ? AvatarMaskBodyPart.LeftFingers : AvatarMaskBodyPart.RightFingers, true);
                    var idle = layer.NewState("Idle");
                    addCache[type] = c => AddClip(c, gesture, idle, layer, type, param, ++paramIndex);
                }
            }

            return addCache[type](clip);
        }

        private static readonly ISet<VFLayer> addedLayers = new HashSet<VFLayer>();

        public bool DidAddLayer(VFLayer layer) {
            return addedLayers.Contains(layer);
        }
        
        private VFAFloat AddClip(
            AnimationClip clip,
            ControllerManager ctrl,
            VFState idle,
            VFLayer layer,
            EditorCurveBindingExtensions.MuscleBindingType type,
            VFAInteger param,
            int paramIndex
        ) {
            clip = clip.Clone();
            var state = layer.NewState(clip.name).WithAnimation(clip);

            var fxParam = floatToDriverService.Drive(param, "FullBodyEmoteService", paramIndex, 0);
            var enableCond = param.IsEqualTo(paramIndex);
            state.TransitionsFromEntry().When(enableCond);
            idle.TransitionsToExit().When(enableCond);

            var outState = layer.NewState($"{clip.name} - Out");
            state.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(enableCond.Not());
            outState.TransitionsToExit().When(ctrl.Always());

            if (type == EditorCurveBindingExtensions.MuscleBindingType.Body) {
                var weightOn = state.AddBehaviour<VRCPlayableLayerControl>();
                weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                weightOn.goalWeight = 1;
                var weightOff = outState.AddBehaviour<VRCPlayableLayerControl>();
                weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                weightOff.goalWeight = 0;
            } else {
                var weightOn = state.AddBehaviour<VRCAnimatorLayerControl>();
                animatorLayerControlManager.Register(weightOn, layer);
                weightOn.goalWeight = 1;
                var weightOff = outState.AddBehaviour<VRCAnimatorLayerControl>();
                animatorLayerControlManager.Register(weightOff, layer);
                weightOff.goalWeight = 0;
            }

            var animOn = state.AddBehaviour<VRCAnimatorTrackingControl>();
            var animOff = outState.AddBehaviour<VRCAnimatorTrackingControl>();
            foreach (var trackingType in TrackingConflictResolverService.allTypes) {
                if (type == EditorCurveBindingExtensions.MuscleBindingType.LeftHand) {
                    if (trackingType != TrackingConflictResolverService.TrackingLeftFingers) {
                        continue;
                    }
                } else if (type == EditorCurveBindingExtensions.MuscleBindingType.RightHand) {
                    if (trackingType != TrackingConflictResolverService.TrackingRightFingers) {
                        continue;
                    }
                } else {
                    if (trackingType == TrackingConflictResolverService.TrackingEyes
                        || trackingType == TrackingConflictResolverService.TrackingMouth
                    ) {
                        continue;
                    }
                }
                trackingType.SetValue(animOn, VRC_AnimatorTrackingControl.TrackingType.Animation);
                trackingType.SetValue(animOff, VRC_AnimatorTrackingControl.TrackingType.Tracking);
            }
            
            return fxParam;
        }
    }
}
