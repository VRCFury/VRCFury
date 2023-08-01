using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * This builder takes all hand gestures and emotes/dances from FX and places them into
     * Gesture / Action where applicable.
     */
    public class PullMusclesOutOfFxBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.PullMusclesOutOfFx)]
        public void Apply() {
            foreach (var layer in GetFx().GetLayers()) {
                ApplyToLayer(layer);
            }
            CreateAltLayers();
        }
        
        private void ApplyToLayer(AnimatorStateMachine layer) {
            var newParams = new Dictionary<AnimatorState, VFAParam>();
            foreach (var state in new AnimatorIterator.States().From(layer)) {
                var newParam = AddToAltLayer(state.motion);
                if (newParam != null) {
                    newParams[state] = newParam;
                }
            }
            if (newParams.Count > 0) {
                foreach (var state in new AnimatorIterator.States().From(layer)) {
                    var driver = state.VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    if (newParams.TryGetValue(state, out var myParam)) {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                            name = myParam.Name(),
                            value = 1
                        });
                    }
                    foreach (var p in newParams.Where(pair => pair.Key != state)) {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                            name = p.Value.Name(),
                            value = 0
                        });
                    }
                }
            }
        }
        
        public enum LayerType {
            Action,
            LeftHand,
            RightHand
        };

        private List<(LayerType, VFAFloat, Motion)> statesToCreate = new List<(LayerType, VFAFloat, Motion)>();

        private void CreateAltLayers() {
            foreach (var group in statesToCreate.GroupBy(state => state.Item1)) {
                var type = group.Key;
                var states = group.Select(tuple => (tuple.Item2, tuple.Item3)).ToArray();
                CreateAltLayer(type, states);
            }
        }

        private void CreateAltLayer(LayerType type, IEnumerable<(VFAFloat,Motion)> states) {
            ControllerManager controller;
            VFALayer layer;
            if (type == LayerType.Action) {
                controller = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Action);
                layer = controller.NewLayer("VRCFury Actions");
                controller.GetRaw().GetLayer(layer.GetRawStateMachine()).weight = 0;
            } else {
                controller = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Gesture);
                layer = controller.NewLayer(type == LayerType.RightHand ? "VRCFury Right Hand" : "VRCFury Left Hand");
                var mask = AvatarMaskExtensions.Empty();
                mask.SetHumanoidBodyPartActive(type == LayerType.RightHand ? AvatarMaskBodyPart.RightFingers : AvatarMaskBodyPart.LeftFingers, true);
                VRCFuryAssetDatabase.SaveAsset(mask, tmpDir, "vrcfGestureMask");
                controller.GetRaw().GetLayer(layer.GetRawStateMachine()).mask = mask;
            }

            var off = layer.NewState("Off");

            var previousStates = new List<(VFACondition, VFAState)>();
            foreach (var s in states) {
                var (param, motion) = s;
                var newState = layer.NewState(motion.name);
                newState.WithAnimation(motion);
                foreach (var (otherCond,other) in previousStates) {
                    newState.TransitionsTo(other).When(otherCond);
                }
                // Because param came from another controller, we have to recreate it
                var myParam = controller.NewFloat(param.Name(), usePrefix: false);
                var myCond = myParam.IsGreaterThan(0);

                var outState = layer.NewState($"{motion.name} - Out");
                newState.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(myCond.Not());
                outState.TransitionsTo(off).When(controller.Always());

                if (type == LayerType.Action) {
                    var weightOn = newState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                    weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOn.goalWeight = 1;
                    var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                    weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOff.goalWeight = 0;
                }


                // TODO: this better
                var trackingOff = newState.GetTrackingControl();
                var trackingOn = outState.GetTrackingControl();

                if (type == LayerType.Action) {
                    trackingOn.trackingHead = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;

                    trackingOff.trackingHead = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                }

                if (type == LayerType.LeftHand) {
                    trackingOn.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;

                    trackingOff.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                }

                if (type == LayerType.RightHand) {
                    trackingOn.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    trackingOn.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOn.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;

                    trackingOff.trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                    trackingOff.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                    trackingOff.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.NoChange;
                }

                off.TransitionsTo(newState).When(myCond);
                previousStates.Add((myCond, newState));
            }
        }

        private int actionNum = 0;

        [CanBeNull]
        private VFAFloat AddToAltLayer(Motion motion) {
            var humanoidMask = GetHumanoidMaskName(motion);
            if (humanoidMask == "none") {
                return null;
            }

            var newParam = GetFx().NewFloat("action_" + (actionNum++));
            if (humanoidMask == "emote") {
                AddToAltLayer(motion, LayerType.Action, newParam);
            } else {
                if (humanoidMask == "hands" || humanoidMask == "leftHand") AddToAltLayer(motion, LayerType.LeftHand, newParam);
                if (humanoidMask == "hands"|| humanoidMask == "rightHand") AddToAltLayer(motion, LayerType.RightHand, newParam);
            }

            return newParam;
        }

        private void AddToAltLayer(Motion motion, LayerType type, VFAFloat param) {
            var copy = mutableManager.CopyRecursive(motion, $"Action from {motion.name}");

            bool ShouldTransferBinding(EditorCurveBinding binding) {
                switch (type) {
                    case LayerType.Action: return binding.IsMuscle();
                    case LayerType.LeftHand: return binding.IsLeftHand();
                    default: return binding.IsRightHand();
                }
            }

            foreach (var clip in new AnimatorIterator.Clips().From(motion)) {
                var deleteBindings = clip.GetFloatBindings().Where(ShouldTransferBinding);
                clip.SetCurves(deleteBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            }
            foreach (var clip in new AnimatorIterator.Clips().From(copy)) {
                var deleteBindings = clip.GetFloatBindings().Where(b => !ShouldTransferBinding(b));
                clip.SetCurves(deleteBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            }
            statesToCreate.Add((type, param, copy));
        }

        private string GetHumanoidMaskName(Motion motion) {

            var leftHand = false;
            var rightHand = false;

            foreach(var clip in new AnimatorIterator.Clips().From(motion)) {
                var bones = AnimationUtility.GetCurveBindings(clip);
                foreach (var b in bones) {
                    if (!(HumanTrait.MuscleName.Contains(b.propertyName) || b.propertyName.EndsWith(" Stretched") || b.propertyName.EndsWith(".Spread"))) continue;
                    if (b.propertyName.Contains("RightHand") || b.propertyName.Contains("Right Thumb") || b.propertyName.Contains("Right Index") ||
                        b.propertyName.Contains("Right Middle") || b.propertyName.Contains("Right Ring") || b.propertyName.Contains("Right Little")) { rightHand = true; continue; }
                    if (b.propertyName.Contains("LeftHand") || b.propertyName.Contains("Left Thumb") || b.propertyName.Contains("Left Index") || 
                        b.propertyName.Contains("Left Middle") || b.propertyName.Contains("Left Ring") || b.propertyName.Contains("Left Little")) { leftHand = true; continue; }
                    return "emote";
                }
            }

            if (leftHand && rightHand) return "hands";
            if (leftHand) return "leftHand";
            if (rightHand) return "rightHand";

            return "none";
        }

        private bool HasAction(Motion motion) {
            return new AnimatorIterator.Clips().From(motion)
                .SelectMany(clip => clip.GetFloatBindings())
                .Any(b => b.IsMuscle() && !b.IsLeftHand() && !b.IsRightHand());
        }
        private bool HasLeftHand(Motion motion) {
            return new AnimatorIterator.Clips().From(motion)
                .SelectMany(clip => clip.GetFloatBindings())
                .Any(b => b.IsLeftHand());
        }
        private bool HasRightHand(Motion motion) {
            return new AnimatorIterator.Clips().From(motion)
                .SelectMany(clip => clip.GetFloatBindings())
                .Any(b => b.IsRightHand());
        }
    }
}
