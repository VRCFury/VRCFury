using System.Linq;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class AddDebugParamService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ExpressionParamsService paramz;
        [VFAutowired] private readonly FixWriteDefaultsService fixWriteDefaultsService;
        
        [FeatureBuilderAction(FeatureOrder.AddDebugVrcParameter)]
        public void Apply() {
            if (!VRCExpressionParameterExtensions.SupportsUnsynced()) {
                return;
            }

            var parts = VrcfDebugLine.GetParts(avatarObject, fixWriteDefaultsService.IsStillBroken());
            var newParams = parts.Select(part => ExpressionParam.Create("VF " + part).Bool().Unsynced());

            paramz.SetParameters(newParams.Concat(paramz));
        }
    }
}
