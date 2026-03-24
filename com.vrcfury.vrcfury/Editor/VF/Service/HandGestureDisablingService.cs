using System;
using System.Collections.Generic;
using System.Linq;
using VF.Feature;
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
            var whenNotInhibitedTree = new Lazy<VFBlendTreeDirect>(() => {
                var output = VFBlendTreeDirect.Create("When gestures not inhibited");
                dbt.Add(BlendtreeMath.GreaterThan(inhibited, 0).create(null, output));
                return output;
            });
            var cachedBuffered = new Dictionary<string, VFAFloat>();
            Action doAtEnd = () => { };

            VFAFloat GetBuffered(string paramName) {
                return cachedBuffered.GetOrCreate(paramName, () => {
                    var buffered = fx.MakeAap(paramName + " (Buffered)");
                    // This is deferred so that the parameter rewrite doesn't rewrite our own usage of the original
                    doAtEnd += () => whenNotInhibitedTree.Value.Add(buffered.MakeCopier(paramName));
                    return buffered;
                });
            }

            fx.RewriteParameters(param => {
                if (RemoveHandGesturesBuilder.GestureParams.Contains(param)) {
                    return GetBuffered(param);
                }
                return param;
            }, includeWrites: false, includeCopyDriverReads: false);

            doAtEnd();
        }
    }
}
