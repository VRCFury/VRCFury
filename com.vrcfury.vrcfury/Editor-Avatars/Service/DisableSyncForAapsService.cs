using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * If an AAP is marked as "synced" in the VRCSDK parameters list, it will break things:
     * 1. The parameter compressor will always think the parameter has changed, because the diff will be comparing the
     * current AAP with the last synced value (which is not the aap)
     * 2. It wastes parameter space because the value will always be overridden by 
     */
    [VFService]
    internal class DisableSyncForAapsService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();

        [FeatureBuilderAction(FeatureOrder.DisableSyncForAaps)]
        public void Apply() {
            var vrcfDrivenAaps = new AnimatorIterator.Clips().From(fx)
                .SelectMany(clip => clip.GetFloatBindings())
                .Where(binding => binding.GetPropType() == EditorCurveBindingType.Aap)
                .Select(binding => binding.propertyName)
                .ToImmutableHashSet();

            foreach (var param in paramz.GetRaw().parameters) {
                if (vrcfDrivenAaps.Contains(param.name)) {
                    Debug.LogWarning($"VRCFury is disabling network sync for {param.name} because it is driven by an AAP");
                    param.SetNetworkSynced(false);
                }
            }
        }
    }
}
