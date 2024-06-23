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
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {
    /**
     * Controller transitions using the wrong parameter type will prevent the controller
     * from loading entirely. Let's just remove those transitions.
     */
    [VFService]
    internal class NoBadControllerParamsBuilder {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.UpgradeWrongParamTypes)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                UpgradeWrongParamTypes(c.GetRaw());
                RemoveWrongParamTypes(c.GetRaw());
            }
        }

        public static void RemoveWrongParamTypes(VFController controller) {
            var badBool = new Lazy<string>(() => controller.NewBool("InvalidParam"));
            var badFloat = new Lazy<string>(() => controller.NewFloat("InvalidParamFloat"));
            var badThreshold = new Lazy<string>(() => controller.NewBool("BadIntThreshold", def: true));

            var paramTypes = controller.parameters
                .ToImmutableDictionary(p => p.name, p => p.type);
            foreach (var transition in new AnimatorIterator.Transitions().From(controller)) {
                var hasBadThreshold = false;
                transition.RewriteConditions(condition => {
                    var mode = condition.mode;
                    var valid = true;
                    if (paramTypes.TryGetValue(condition.parameter, out var type)) {
                        if (type == AnimatorControllerParameterType.Bool ||
                            type == AnimatorControllerParameterType.Trigger) {
                            valid = mode == AnimatorConditionMode.If || mode == AnimatorConditionMode.IfNot;

                            // When you use a bool with an incorrect mode, the editor just always says "True",
                            // so let's just actually make it do that instead of converting it to InvalidParamType
                            if (!valid) {
                                condition.mode = AnimatorConditionMode.If;
                                valid = true;
                            }
                        }

                        if (type == AnimatorControllerParameterType.Int) {
                            valid = mode == AnimatorConditionMode.Equals
                                    || mode == AnimatorConditionMode.NotEqual
                                    || mode == AnimatorConditionMode.Greater
                                    || mode == AnimatorConditionMode.Less;
                            
                             // When you use an int with a float threshold, the editor shows the floor value,
                             // but evaluates the condition using the original value. Let's fix that so the editor
                             // valus is actually the one that is used.
                             var floored = (int)Math.Floor(condition.threshold);
                             if (condition.threshold != floored) {
                                 condition.threshold = floored;
                                 hasBadThreshold = true;
                             }
                        }

                        if (type == AnimatorControllerParameterType.Float) {
                            valid = mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less;
                        }
                    } else {
                        valid = false;
                    }

                    if (!valid) {
                        condition.parameter = badBool.Value;
                        condition.mode = AnimatorConditionMode.If;
                    }

                    return condition;
                });

                if (hasBadThreshold) {
                    transition.AddCondition(AnimatorConditionMode.If, 0, badThreshold.Value);
                }
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
            
            // Change the param types
            controller.parameters = controller.parameters.Select(p => {
                if (paramTypes.TryGetValue(p.name, out var type)) {
                    float oldDefault = 0;
                    if (p.type == AnimatorControllerParameterType.Bool) oldDefault = p.defaultBool ? 1 : 0;
                    if (p.type == AnimatorControllerParameterType.Int) oldDefault = p.defaultInt;
                    if (p.type == AnimatorControllerParameterType.Float) oldDefault = p.defaultFloat;
                    p.type = type;
                    p.defaultBool = oldDefault > 0;
                    p.defaultInt = (int)Math.Round(oldDefault);
                    p.defaultFloat = oldDefault;
                }
                return p;
            }).ToArray();

            // Fix all of the usages
            foreach (var layer in controller.GetLayers()) {
                AnimatorIterator.ForEachTransitionRW(layer, transition => {
                    var output = new List<AnimatorCondition>();
                    var changed = false;
                    var flip = new List<int>();
                    foreach (var _c in transition.conditions) {
                        var c = _c;
                        var mode = c.mode;
                        if (!paramTypes.TryGetValue(c.parameter, out var type)) {
                            output.Add(c);
                            continue;
                        }
                        if (type == AnimatorControllerParameterType.Float) {
                            if (mode == AnimatorConditionMode.Equals) {
                                c.mode = AnimatorConditionMode.Greater;
                                c.threshold = _c.threshold - 0.001f;
                                output.Add(c);
                                c.mode = AnimatorConditionMode.Less;
                                c.threshold = _c.threshold + 0.001f;
                                output.Add(c);
                                changed = true;
                                continue;
                            }
                            if (mode == AnimatorConditionMode.NotEqual) {
                                flip.Add(output.Count);
                                c.mode = AnimatorConditionMode.Greater;
                                c.threshold = _c.threshold;
                                output.Add(c);
                                changed = true;
                                continue;
                            }
                        }
                        if (type == AnimatorControllerParameterType.Int || type == AnimatorControllerParameterType.Float) {
                            if (mode == AnimatorConditionMode.If) {
                                c.mode = AnimatorConditionMode.Greater;
                                c.threshold = 0;
                                changed = true;
                            }
                            if (mode == AnimatorConditionMode.IfNot) {
                                c.mode = AnimatorConditionMode.Less;
                                c.threshold = (type == AnimatorControllerParameterType.Float ? 0.001f : 1f);
                                changed = true;
                            }
                        }
                        output.Add(c);
                    }
                    if (changed) transition.conditions = output.ToArray();

                    var outputTransitions = new List<AnimatorTransitionBase>();
                    outputTransitions.Add(transition);
                    foreach (var combo in GetCombinations(flip)) {
                        if (combo.Length == 0) continue;
                        var copy = transition.Clone();
                        var cs = copy.conditions;
                        foreach (var i in combo) {
                            var c = cs[i];
                            c.mode = AnimatorConditionMode.Less;
                            cs[i] = c;
                        }
                        copy.conditions = cs;
                        outputTransitions.Add(copy);
                    }
                    return outputTransitions;
                });
            }
        }
        
        // https://stackoverflow.com/questions/64998630/get-all-combinations-of-liststring-where-order-doesnt-matter-and-minimum-of-2
        private static IEnumerable<T[]> GetCombinations<T>(List<T> source) {
            BigInteger one = 1;
            for (BigInteger i = 0; i < one << source.Count; i++)
                yield return source.Where((_, j) => (i & one << j) != 0).ToArray();
        }
    }
}
