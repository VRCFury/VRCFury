using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;

namespace VF.Feature {
    /**
     * Controller transitions using the wrong parameter type will prevent the controller
     * from loading entirely. Let's just remove those transitions.
     */
    [VFService]
    public class NoBadControllerParamsBuilder {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.RemoveBadControllerTransitions)]
        public void Apply() {
            foreach (var c in manager.GetAllUsedControllers()) {
                var paramTypes = c.GetRaw().parameters
                    .ToImmutableDictionary(p => p.name, p => p.type);
                foreach (var transition in new AnimatorIterator.Transitions().From(c.GetRaw())) {
                    transition.conditions = transition.conditions.Select(condition => {
                        var mode = condition.mode;
                        var valid = true;
                        if (paramTypes.TryGetValue(condition.parameter, out var type)) {
                            if (type == AnimatorControllerParameterType.Bool ||
                                type == AnimatorControllerParameterType.Trigger) {
                                valid = mode == AnimatorConditionMode.If || mode == AnimatorConditionMode.IfNot;
                            }

                            if (type == AnimatorControllerParameterType.Int) {
                                valid = mode == AnimatorConditionMode.Equals
                                        || mode == AnimatorConditionMode.NotEqual
                                        || mode == AnimatorConditionMode.Greater
                                        || mode == AnimatorConditionMode.Less;
                            }

                            if (type == AnimatorControllerParameterType.Float) {
                                valid = mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less;
                            }
                        } else {
                            valid = false;
                        }

                        if (!valid) {
                            condition.parameter = c.NewBool("InvalidParamType", usePrefix: false).Name();
                            condition.mode = AnimatorConditionMode.If;
                        }

                        return condition;
                    }).ToArray();
                }
            }
        }
    }
}
