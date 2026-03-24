using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class MarkThingsAsDirtyJustInCaseService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly MenuService menuService;
        [VFAutowired] private readonly ParamsService paramsService;
        
        [FeatureBuilderAction(FeatureOrder.MarkThingsAsDirtyJustInCase)]
        public void Apply() {
            // Just for safety. These don't need to be here if we make sure everywhere else appropriately marks
            foreach (var c in controllers.GetAllUsedControllers()) {
                c.GetRaw().Dirty();
            }
            menuService.GetMenu().GetRaw().Dirty();
            paramsService.GetParams().GetRaw().Dirty();
        }
    }
}
