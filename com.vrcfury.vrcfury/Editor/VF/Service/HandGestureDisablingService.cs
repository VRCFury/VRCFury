using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class HandGestureDisablingService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        
        private readonly List<VFAFloat> inhibitors = new List<VFAFloat>();
        
        public VFAFloat AddInhibitor(string owner) {
            var param = fx.NewFloat($"DG_{owner}");
            inhibitors.Add(param);
            return param;
        }

        [FeatureBuilderAction(FeatureOrder.DisableGesturesService)]
        public void Apply() {
            if (!inhibitors.Any()) return;
            var dbt = dbtLayerService.Create();
            var math = dbtLayerService.GetMath(dbt);
            var inhibited = math.Add(
                "Inhibit Gestures",
                inhibitors.Select(i => ((BlendtreeMath.VFAFloatOrConst)i, 1f)).ToArray()
            );
            var gesturesEnabled = new AnimatorCondition {
                parameter = inhibited,
                mode = AnimatorConditionMode.IfNot
            };
            var gesturesDisabled = new AnimatorCondition {
                parameter = inhibited,
                mode = AnimatorConditionMode.If
            };
            foreach (var layer in fx.GetLayers()) {
                layer.RewriteConditions(c => {
                    if (c.IsForGesture()) {
                        if (c.IncludesValue(0)) {
                            return AnimatorTransitionBaseExtensions.Rewritten.Or(c, gesturesDisabled);
                        } else {
                            return AnimatorTransitionBaseExtensions.Rewritten.And(c, gesturesEnabled);
                        }
                    }
                    return c;
                });
            }
        }
    }
}
