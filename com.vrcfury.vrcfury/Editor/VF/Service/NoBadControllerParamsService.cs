using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Controller transitions using the wrong parameter type will prevent the controller
     * from loading entirely. Let's just remove those transitions.
     */
    [VFService]
    internal class NoBadControllerParamsService {
        [VFAutowired] private readonly ControllersService controllers;
        
        [FeatureBuilderAction(FeatureOrder.UpgradeWrongParamTypes)]
        public void Apply() {
            foreach (var c in controllers.GetAllUsedControllers()) {
                foreach (var tree in new AnimatorIterator.Trees().From(c)) {
                    if (tree.blendType == BlendTreeType.Direct) {
                        tree.RewriteChildren(child => {
                            if (child.directBlendParameter == VFBlendTreeDirect.AlwaysOneParam) {
                                child.directBlendParameter = c.One();
                            }
                            return child;
                        });
                    }
                }
                UpgradeWrongParamTypes(c);
                RemoveWrongParamTypes(c);
            }
        }

        public static void RemoveWrongParamTypes(VFController controller) {
            var badBool = new Lazy<string>(() => controller._NewBool("InvalidParam"));
            var badFloat = new Lazy<string>(() => controller._NewFloat("InvalidParamFloat"));
            var badThreshold = new Lazy<string>(() => controller._NewBool("BadIntThreshold", def: true));
            AnimatorCondition InvalidCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = badBool.Value,
            };
            AnimatorCondition BadThresholdCondition() => new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = badThreshold.Value,
            };

            var paramTypes = controller.parameters
                .ToImmutableDictionary(p => p.name, p => p.type);
            foreach (var layer in controller.GetLayers()) {
                AnimatorIterator.RewriteConditions(layer, condition => {
                    var mode = condition.mode;

                    if (!paramTypes.TryGetValue(condition.parameter, out var type)) {
                        return InvalidCondition();
                    }

                    if (type == AnimatorControllerParameterType.Bool || type == AnimatorControllerParameterType.Trigger) {
                        // When you use a bool with an incorrect mode, the editor just always says "True",
                        // so let's just actually make it do that instead of converting it to InvalidParamType
                        if (mode != AnimatorConditionMode.If && mode != AnimatorConditionMode.IfNot) {
                            condition.mode = AnimatorConditionMode.If;
                            return condition;
                        }
                    } else if (type == AnimatorControllerParameterType.Int) {
                        if (mode != AnimatorConditionMode.Equals
                            && mode != AnimatorConditionMode.NotEqual
                            && mode != AnimatorConditionMode.Greater
                            && mode != AnimatorConditionMode.Less) {
                            return InvalidCondition();
                        }

                        // When you use an int with a float threshold, the editor shows the floor value,
                        // but evaluates the condition using the original value. Let's fix that so the editor
                        // value is actually the one that is used.
                        var floored = (int)Math.Floor(condition.threshold);
                        if (condition.threshold != floored) {
                            condition.threshold = floored;
                            return AnimatorTransitionBaseExtensions.Rewritten.And(
                                condition,
                                BadThresholdCondition()
                            );
                        }
                    } else if (type == AnimatorControllerParameterType.Float) {
                        if (mode != AnimatorConditionMode.Greater && mode != AnimatorConditionMode.Less) {
                            return InvalidCondition();
                        }
                    }

                    return condition;
                });
            }

            bool IsFloat(string p) =>
                p != null && paramTypes.TryGetValue(p, out var type) && type == AnimatorControllerParameterType.Float;
            bool IsBool(string p) =>
                p != null && paramTypes.TryGetValue(p, out var type) && type == AnimatorControllerParameterType.Bool;

            foreach (var tree in new AnimatorIterator.Trees().From(controller)) {
                tree.RewriteParameters(p => {
                    if (!IsFloat(p)) return badFloat.Value;
                    return p;
                });
            }

            foreach (var state in new AnimatorIterator.States().From(controller)) {
                if (state.mirrorParameterActive && !IsBool(state.mirrorParameter))
                    state.mirrorParameter = badBool.Value;
                if (state.speedParameterActive && !IsFloat(state.speedParameter))
                    state.speedParameter = badFloat.Value;
                if (state.timeParameterActive && !IsFloat(state.timeParameter))
                    state.timeParameter = badFloat.Value;
                if (state.cycleOffsetParameterActive && !IsFloat(state.cycleOffsetParameter))
                    state.cycleOffsetParameter = badFloat.Value;
            }
            
            controller.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                if (binding.GetPropType() == EditorCurveBindingType.Aap && !IsFloat(binding.propertyName)) {
                    return null;
                }
                return binding;
            }));
        }

        /**
         * "Upgrades" all parameters to the highest "type" needed for all usages, then makes all usages
         * work properly.
         *
         * For instance, if a parameter is used in both If and as a direct blendtree parameter,
         * it will be set to type Float, and the If will be converted to Greater than 0.
         */
        public static void UpgradeWrongParamTypes(VFController controller) {
            // Figure out what types each param needs to be (at least)
            var paramTypes = new Dictionary<string, AnimatorControllerParameterType>();
            void UpgradeType(string name, AnimatorControllerParameterType newType) {
                if (!paramTypes.TryGetValue(name, out var type)) type = newType;
                else if (newType == AnimatorControllerParameterType.Float) type = newType;
                else if (newType == AnimatorControllerParameterType.Int && (type == AnimatorControllerParameterType.Bool || type == AnimatorControllerParameterType.Trigger)) type = newType;
                else if (newType == AnimatorControllerParameterType.Bool && type == AnimatorControllerParameterType.Trigger) type = newType;
                paramTypes[name] = type;
            }
            foreach (var p in controller.parameters) {
                UpgradeType(p.name, p.type);
            }
            foreach (var condition in new AnimatorIterator.Conditions().From(controller)) {
                var mode = condition.mode;
                if (mode == AnimatorConditionMode.Equals || mode == AnimatorConditionMode.NotEqual) {
                    UpgradeType(condition.parameter, AnimatorControllerParameterType.Int);
                }
                if (mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less) {
                    if (condition.threshold % 1 == 0) {
                        UpgradeType(condition.parameter, AnimatorControllerParameterType.Int);
                    } else {
                        UpgradeType(condition.parameter, AnimatorControllerParameterType.Float);
                    }
                }
            }
            foreach (var tree in new AnimatorIterator.Trees().From(controller)) {
                tree.RewriteParameters(p => {
                    UpgradeType(p, AnimatorControllerParameterType.Float);
                    return p;
                });
            }
            foreach (var state in new AnimatorIterator.States().From(controller)) {
                if (state.speedParameterActive)
                    UpgradeType(state.speedParameter, AnimatorControllerParameterType.Float);
                if (state.timeParameterActive)
                    UpgradeType(state.timeParameter, AnimatorControllerParameterType.Float);
                if (state.cycleOffsetParameterActive)
                    UpgradeType(state.cycleOffsetParameter, AnimatorControllerParameterType.Float);
            }
            foreach (var clip in new AnimatorIterator.Clips().From(controller)) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.GetPropType() == EditorCurveBindingType.Aap) {
                        UpgradeType(binding.propertyName, AnimatorControllerParameterType.Float);
                    }
                }
            }
            
            // Change the param types
            controller.parameters = controller.parameters.Select(p => {
                if (paramTypes.TryGetValue(p.name, out var type)) {
                    var oldDefault = p.GetDefaultValueAsFloat();
                    p.type = type;
                    p.defaultBool = oldDefault > 0;
                    p.defaultInt = (int)Math.Round(oldDefault);
                    p.defaultFloat = oldDefault;
                }
                return p;
            }).ToArray();

            // Fix all of the usages
            foreach (var layer in controller.GetLayers()) {
                AnimatorIterator.RewriteConditions(layer, c => {
                    if (!paramTypes.TryGetValue(c.parameter, out var type)) {
                        return c;
                    }
                    if (type == AnimatorControllerParameterType.Int || type == AnimatorControllerParameterType.Float) {
                        if (c.mode == AnimatorConditionMode.If) {
                            c.mode = AnimatorConditionMode.NotEqual;
                            c.threshold = 0;
                        }
                        if (c.mode == AnimatorConditionMode.IfNot) {
                            c.mode = AnimatorConditionMode.Equals;
                            c.threshold = 0;
                        }
                    }
                    if (type == AnimatorControllerParameterType.Float) {
                        if (c.mode == AnimatorConditionMode.Equals) {
                            return AnimatorTransitionBaseExtensions.Rewritten.And(
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Greater, threshold = c.threshold - 0.001f },
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Less, threshold = c.threshold + 0.001f }
                            );
                        }
                        if (c.mode == AnimatorConditionMode.NotEqual) {
                            return AnimatorTransitionBaseExtensions.Rewritten.Or(
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Less, threshold = c.threshold - 0.001f },
                                new AnimatorCondition { parameter = c.parameter, mode = AnimatorConditionMode.Greater, threshold = c.threshold + 0.001f }
                            );
                        }
                    }
                    return c;
                });
            }
        }
    }
}
