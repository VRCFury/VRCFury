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
                var newParam = AddToAltLayer(state);
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

                off.TransitionsTo(newState).When(myCond);
                previousStates.Add((myCond, newState));
            }
        }

        private int actionNum = 0;

        [CanBeNull]
        private VFAFloat AddToAltLayer(AnimatorState state) {
            var motion = state.motion;
            if (motion == null) return null;

            var hasAction = HasAction(motion);
            var hasLeftHand = HasLeftHand(motion);
            var hasRightHand = HasRightHand(motion);
            if (!hasAction && !hasLeftHand && !hasRightHand) return null;

            var newParam = GetFx().NewFloat("action_" + (actionNum++));
            if (hasAction) {
                AddToAltLayer(state, LayerType.Action, newParam);
            } else {
                if (hasLeftHand) AddToAltLayer(state, LayerType.LeftHand, newParam);
                if (hasRightHand) AddToAltLayer(state, LayerType.RightHand, newParam);
            }

            return newParam;
        }

        private void AddToAltLayer(AnimatorState state, LayerType type, VFAFloat param) {
            var originalMotion = state.motion;

            bool ShouldTransferBinding(EditorCurveBinding binding) {
                switch (type) {
                    case LayerType.Action: return binding.IsMuscle();
                    case LayerType.LeftHand: return binding.IsLeftHand();
                    default: return binding.IsRightHand();
                }
            }

            var copyWithoutMuscles = mutableManager.CopyRecursive(originalMotion, $"{originalMotion.name} (no muscles)");
            foreach (var clip in new AnimatorIterator.Clips().From(copyWithoutMuscles)) {
                var deleteBindings = clip.GetFloatBindings().Where(ShouldTransferBinding);
                clip.SetCurves(deleteBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            }
            var copyOnlyMuscles = mutableManager.CopyRecursive(originalMotion, $"{originalMotion.name} (only muscles)");
            foreach (var clip in new AnimatorIterator.Clips().From(copyOnlyMuscles)) {
                var deleteBindings = clip.GetFloatBindings().Where(b => !ShouldTransferBinding(b));
                clip.SetCurves(deleteBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            }

            state.motion = copyWithoutMuscles;
            statesToCreate.Add((type, param, copyOnlyMuscles));
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
