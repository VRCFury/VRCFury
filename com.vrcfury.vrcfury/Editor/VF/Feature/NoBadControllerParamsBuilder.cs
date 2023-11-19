using System.Collections.Immutable;
using System.Linq;
using Editor.VF.Utils;
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
                    transition.RewriteConditions(condition => {
                        var mode = condition.mode;
                        var valid = true;
                      if (paramTypes.TryGetValue(condition.parameter, out var type)) {
                            switch (type) {
                                case AnimatorControllerParameterType.Bool:
                                case AnimatorControllerParameterType.Trigger:
                                {
                                    valid = mode == AnimatorConditionMode.If || mode == AnimatorConditionMode.IfNot;

                                    // When you use a bool with an incorrect mode, the editor just always says "True",
                                    // so let's just actually make it do that instead of converting it to InvalidParamType
                                    if (!valid) {
                                        condition.mode = AnimatorConditionMode.If;
                                        valid = true;
                                    }

                                    break;
                                }
                                case AnimatorControllerParameterType.Int:
                                    valid = mode == AnimatorConditionMode.Equals
                                            || mode == AnimatorConditionMode.NotEqual
                                            || mode == AnimatorConditionMode.Greater
                                            || mode == AnimatorConditionMode.Less;
                                    break;
                                case AnimatorControllerParameterType.Float:
                                    valid = mode == AnimatorConditionMode.Greater || mode == AnimatorConditionMode.Less;
                                    break;
                            }
                        } else {
                            valid = false;
                        }id = false;
                        }

                        if (!valid) {
                            condition.parameter = c.NewBool("InvalidParamType", usePrefix: false).Name();
                            condition.mode = AnimatorConditionMode.If;
                        }

                        return condition;
                    });
                }
            }
        }
    }
}
