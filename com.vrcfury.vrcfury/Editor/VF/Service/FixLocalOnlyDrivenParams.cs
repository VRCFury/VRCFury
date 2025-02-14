using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    internal class FixLocalOnlyDrivenParams {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        [FeatureBuilderAction(FeatureOrder.LocalOnlyDrivenParams)]
        public void Apply() {
            var syncedParams = paramz.GetRaw().parameters.Where(p => p.IsNetworkSynced()).Select(p => p.name).ToList();
            
            foreach (var controller in controllers.GetAllUsedControllers()) {
                foreach (var behaviour in new AnimatorIterator.Behaviours().From(controller.GetRaw())) {
                    if (behaviour is VRCAvatarParameterDriver driver) {
                        foreach (var p in driver.parameters) {
                            if (syncedParams.Contains(p.name)) {
                                driver.localOnly = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
