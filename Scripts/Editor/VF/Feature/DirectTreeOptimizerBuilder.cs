using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace VF.Feature {
    public class DirectTreeOptimizerBuilder : FeatureBuilder<DirectTreeOptimizer> {
        [FeatureBuilderAction(FeatureOrder.DirectTreeOptimizer)]
        public void Apply() {
            var fx = GetFx();

            var bindingsByLayer = fx.GetLayers()
                .ToDictionary(layer => layer, layer => GetBindingsAnimatedInLayer(layer));
            
            var eligibleLayers = new List<EligibleLayer>();
            foreach (var layer in fx.GetLayers()) {
                if (layer.states.Length != 2) continue;
                if (layer.stateMachines.Length != 0) continue;
                var state0 = layer.states[0].state;
                var state1 = layer.states[1].state;
                if (state0.behaviours.Length > 0 || state1.behaviours.Length > 0) continue;

                var allTransitions = new List<AnimatorTransitionBase>();
                allTransitions.AddRange(layer.entryTransitions);
                allTransitions.AddRange(layer.anyStateTransitions);
                allTransitions.AddRange(state0.transitions);
                allTransitions.AddRange(state1.transitions);

                var state0Condition =
                    GetSingleCondition(allTransitions.Where(t => t.destinationState == state0 || (t.isExit && layer.defaultState == state0)));
                var state1Condition =
                    GetSingleCondition(allTransitions.Where(t => t.destinationState == state1 || (t.isExit && layer.defaultState == state1)));
                if (state0Condition == null || state1Condition == null) continue;
                if (state0Condition.Value.parameter != state1Condition.Value.parameter) continue;

                var state0EffectiveCondition = EstimateEffectiveCondition(state0Condition.Value);
                var state1EffectiveCondition = EstimateEffectiveCondition(state1Condition.Value);

                AnimatorState onState;
                AnimatorState offState;
                if (
                    state0EffectiveCondition == EffectiveCondition.WHEN_0 &&
                    state1EffectiveCondition == EffectiveCondition.WHEN_1) {
                    offState = state0;
                    onState = state1;
                } else if (
                    state0EffectiveCondition == EffectiveCondition.WHEN_1 &&
                    state1EffectiveCondition == EffectiveCondition.WHEN_0) {
                    offState = state1;
                    onState = state0;
                } else {
                    continue;
                }

                var usedBindings = bindingsByLayer[layer];
                var someOtherLayerAnimatesTheSameThing = bindingsByLayer
                    .Any(pair => pair.Key != layer && pair.Value.Any(b => usedBindings.Contains(b)));
                if (someOtherLayerAnimatesTheSameThing) {
                    continue;
                }

                eligibleLayers.Add(new EligibleLayer {
                    offState = GetMotionIfNotEmpty(offState),
                    onState = GetMotionIfNotEmpty(onState),
                    param = state0Condition.Value.parameter
                });

                Debug.LogWarning("Removing " + layer);
                fx.RemoveLayer(layer);
            }

            if (eligibleLayers.Count > 0) {
                var tree = manager.GetClipStorage().NewBlendTree("Optimized Toggles");
                tree.blendType = BlendTreeType.Direct;
                foreach (var toggle in eligibleLayers) {
                    if (!toggle.offState && !toggle.onState) continue;
                    string param;
                    Motion motion;
                    if (toggle.offState) {
                        var subTree = manager.GetClipStorage().NewBlendTree("Optimized Toggle " + toggle.offState);
                        subTree.useAutomaticThresholds = false;
                        subTree.blendType = BlendTreeType.Simple1D;
                        subTree.AddChild(toggle.offState, 0);
                        subTree.AddChild(
                            toggle.onState != null ? toggle.onState : manager.GetClipStorage().GetNoopClip(), 1);
                        subTree.blendParameter = toggle.param;
                        tree.AddChild(subTree);
                        param = fx.True().Name();
                        motion = subTree;
                    } else {
                        param = toggle.param;
                        motion = toggle.onState;
                    }

                    tree.AddChild(motion);
                    var children = tree.children;
                    var child = children[children.Length - 1];
                    child.directBlendParameter = param;
                    children[children.Length - 1] = child;
                    tree.children = children;

                    var fxRaw = fx.GetRaw();
                    fxRaw.parameters = fxRaw.parameters.Select(p => {
                        if (p.name == toggle.param) {
                            p.type = AnimatorControllerParameterType.Float;
                        }
                        return p;
                    }).ToArray();
                }
                
                var layer = fx.NewLayer("Optimized Toggles");
                layer.NewState("Optimized Toggles").WithAnimation(tree);
            }
        }

        private Motion GetMotionIfNotEmpty(AnimatorState state) {
            var hasBinding = false;
            AnimatorIterator.ForEachClip(state, clip => {
                hasBinding |= AnimationUtility.GetCurveBindings(clip).Length > 0;
                hasBinding |= AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0;
            });
            if (!hasBinding) return null;
            return state.motion;
        }

        private ICollection<EditorCurveBinding> GetBindingsAnimatedInLayer(AnimatorStateMachine sm) {
            var usedBindings = new HashSet<EditorCurveBinding>();
            AnimatorIterator.ForEachClip(sm, clip => {
                usedBindings.UnionWith(AnimationUtility.GetCurveBindings(clip));
                usedBindings.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
            });
            return usedBindings;
        }

        public enum EffectiveCondition {
            WHEN_1,
            WHEN_0,
            INVALID
        }
        private static EffectiveCondition EstimateEffectiveCondition(AnimatorCondition cond) {
            if (cond.mode == AnimatorConditionMode.If) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.IfNot) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.Equals && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Equals && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.NotEqual && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_0;
            if (cond.mode == AnimatorConditionMode.NotEqual && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Greater && Mathf.Approximately(cond.threshold, 0)) return EffectiveCondition.WHEN_1;
            if (cond.mode == AnimatorConditionMode.Less && Mathf.Approximately(cond.threshold, 1)) return EffectiveCondition.WHEN_0;
            return EffectiveCondition.INVALID;
        }

        private static AnimatorCondition? GetSingleCondition(IEnumerable<AnimatorTransitionBase> transitions) {
            var allConditions = transitions
                .SelectMany(t => t.conditions)
                .Distinct()
                .ToList();
            if (allConditions.Count != 1) return null;
            return allConditions[0];
        }

        public class EligibleLayer {
            public Motion offState;
            public Motion onState;
            public string param;
        }
        
        public override string GetEditorTitle() {
            return "Direct Tree Optimizer";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically convert all non-conflicting toggle layers into a single direct blend tree layer."
            ));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}