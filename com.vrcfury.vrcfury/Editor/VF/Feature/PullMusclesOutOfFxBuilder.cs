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
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * This builder takes all hand gestures and emotes/dances from FX and places them into
     * Gesture / Action where applicable.
     */
    public class PullMusclesOutOfFxBuilder : FeatureBuilder {
        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder animatorLayerControlManager;
        
        [FeatureBuilderAction(FeatureOrder.PullMusclesOutOfFx)]
        public void Apply() {
            var fx = GetFx();
            foreach (var layer in fx.GetManagedLayers()) {
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

        private List<(LayerType, VFABool, Motion)> statesToCreate = new List<(LayerType, VFABool, Motion)>();

        private void CreateAltLayers() {
            foreach (var group in statesToCreate.GroupBy(state => state.Item1)) {
                var type = group.Key;
                var states = group.Select(tuple => (tuple.Item2, tuple.Item3)).ToArray();
                CreateAltLayer(type, states);
            }
        }

        private void CreateAltLayer(LayerType type, IEnumerable<(VFABool,Motion)> states) {
            ControllerManager controller;
            VFLayer layer;
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
                controller.GetManagedLayers().First(l => l.stateMachine == layer.GetRawStateMachine()).weight = 0;
            }

            var off = layer.NewState("Off");

            var previousStates = new List<(VFCondition, VFState)>();
            foreach (var s in states) {
                var (param, motion) = s;
                var newState = layer.NewState(motion.name);
                newState.WithAnimation(motion);
                // Because param came from another controller, we have to recreate it
                var myParam = controller.NewBool(param.Name(), usePrefix: false);
                var myCond = myParam.IsTrue();

                var outState = layer.NewState($"{motion.name} - Out");
                off.TransitionsToExit().When(myCond);
                newState.TransitionsFromEntry().When(myCond);
                newState.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(myCond.Not());
                newState.TransitionsTo(newState).WithTransitionExitTime(1).When(); 
                foreach (var (otherCond,other) in previousStates) {
                    newState.TransitionsToExit().When(otherCond);
                }
                outState.TransitionsToExit().When(controller.Always());

                if (type == LayerType.Action) {
                    var weightOn = newState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                    weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOn.goalWeight = 1;
                    var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
                    weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    weightOff.goalWeight = 0;
                } else {
                    var weightOn = newState.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    weightOn.goalWeight = 1;
                    animatorLayerControlManager.Register(weightOn, layer.GetRawStateMachine());
                    var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    weightOff.goalWeight = 0;
                    animatorLayerControlManager.Register(weightOff, layer.GetRawStateMachine());
                }

                previousStates.Add((myCond, newState));
            }

            off.TransitionsFromEntry().When(controller.Always());
        }

        private int actionNum = 0;

        [CanBeNull]
        private VFABool AddToAltLayer(AnimatorState state) {
            var motion = state.motion;
            if (motion == null) return null;

            var muscleTypes = new AnimatorIterator.Clips().From(motion)
                .SelectMany(clip => clip.GetMuscleBindingTypes())
                .ToImmutableHashSet();

            List<(AnimationClip,bool)> proxies;
            if (motion is AnimationClip rootClip) {
                proxies = rootClip.CollapseProxyBindings(true);
            } else {
                proxies = new List<(AnimationClip, bool)>();
            }

            if (muscleTypes.Count == 0 && proxies.Count == 0) return null;

            var newParam = GetFx().NewBool("action_" + (actionNum++));
            if (muscleTypes.Contains(EditorCurveBindingExtensions.MuscleBindingType.Other)) {
                AddToAltLayer(state, LayerType.Action, newParam);
            } else {
                if (muscleTypes.Contains(EditorCurveBindingExtensions.MuscleBindingType.LeftHand))
                    AddToAltLayer(state, LayerType.LeftHand, newParam);
                if (muscleTypes.Contains(EditorCurveBindingExtensions.MuscleBindingType.RightHand))
                    AddToAltLayer(state, LayerType.RightHand, newParam);
            }

            foreach (var proxy in proxies) {
                var (proxyClip, isAction) = proxy;
                if (isAction) {
                    statesToCreate.Add((LayerType.Action, newParam, proxyClip));
                } else {
                    statesToCreate.Add((LayerType.LeftHand, newParam, proxyClip));
                    statesToCreate.Add((LayerType.RightHand, newParam, proxyClip));
                }
            }

            return newParam;
        }

        private void AddToAltLayer(AnimatorState state, LayerType type, VFABool param) {
            var originalMotion = state.motion;

            bool ShouldTransferBinding(EditorCurveBinding binding) {
                switch (type) {
                    case LayerType.Action: return binding.IsMuscle();
                    case LayerType.LeftHand: return binding.GetMuscleBindingType() == EditorCurveBindingExtensions.MuscleBindingType.LeftHand;
                    default: return binding.GetMuscleBindingType() == EditorCurveBindingExtensions.MuscleBindingType.RightHand;
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
    }
}
