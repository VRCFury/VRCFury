using System;
using System.Collections.Generic;
using UnityEngine;
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
        
        private readonly Dictionary<VFBinding.MuscleBindingType, Func<VFClip,VFAFloat>> addCache
            = new Dictionary<VFBinding.MuscleBindingType, Func<VFClip, VFAFloat>>();

        public VFAFloat AddClip(VFClip clip, VFBinding.MuscleBindingType type) {
            if (!addCache.ContainsKey(type)) {
                if (type == VFBinding.MuscleBindingType.Body) {
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
                    var isLeft = type == VFBinding.MuscleBindingType.LeftHand;
                    var name = "VRCFury " + (isLeft ? "Left Hand" : "Right Hand");
                    var param = gesture.NewInt(name);
                    var paramIndex = 0;
                    var layer = gesture.NewLayer(name);
                    addedLayers.Add(layer);
                    layer.weight = 0;
                    layer.mask = VFMask.Empty();
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
            VFClip clip,
            ControllerManager ctrl,
            VFState idle,
            VFLayer layer,
            VFBinding.MuscleBindingType type,
            VFAInteger param,
            int paramIndex
        ) {
            clip = clip.Clone() as VFClip;
            var state = layer.NewState(clip.name).WithAnimation(clip);

            var fxParam = floatToDriverService.Drive(param, "FullBodyEmoteService", paramIndex, 0);
            var enableCond = param.IsEqualTo(paramIndex);
            state.TransitionsFromEntry().When(enableCond);
            idle.TransitionsToExit().When(enableCond);

            var outState = layer.NewState($"{clip.name} - Out");
            state.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(enableCond.Not());
            outState.TransitionsToExit().When(ctrl.Always());

            if (type == VFBinding.MuscleBindingType.Body) {
                state.behaviours.AddBehaviour<VRCPlayableLayerControl>(weightOn => {
                    weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOn.goalWeight = 1;
                });
                outState.behaviours.AddBehaviour<VRCPlayableLayerControl>(weightOff => {
                    weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOff.goalWeight = 0;
                });
            } else {
                var weightOn = state.behaviours.AddBehaviour<VRCAnimatorLayerControl>(weightOn => {
                    weightOn.goalWeight = 1;
                });
                animatorLayerControlManager.Register(weightOn, layer);

                var weightOff = outState.behaviours.AddBehaviour<VRCAnimatorLayerControl>(weightOff => {
                    weightOff.goalWeight = 0;
                });
                animatorLayerControlManager.Register(weightOff, layer);
            }

            state.behaviours.AddBehaviour<VRCAnimatorTrackingControl>(animOn => {
                foreach (var trackingType in TrackingConflictResolverService.allTypes) {
                    if (type == VFBinding.MuscleBindingType.LeftHand) {
                        if (trackingType != TrackingConflictResolverService.TrackingLeftFingers) {
                            continue;
                        }
                    } else if (type == VFBinding.MuscleBindingType.RightHand) {
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
                }
            });
            outState.behaviours.AddBehaviour<VRCAnimatorTrackingControl>(animOff => {
                foreach (var trackingType in TrackingConflictResolverService.allTypes) {
                    if (type == VFBinding.MuscleBindingType.LeftHand) {
                        if (trackingType != TrackingConflictResolverService.TrackingLeftFingers) {
                            continue;
                        }
                    } else if (type == VFBinding.MuscleBindingType.RightHand) {
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
                    trackingType.SetValue(animOff, VRC_AnimatorTrackingControl.TrackingType.Tracking);
                }
            });
            
            return fxParam;
        }
    }
}
