using System.Linq;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * Having these in the expressions param list literally just wastes space
     */
    [VFService]
    public class RemoveVrcGlobalsFromExpressionParamsService {
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();

        [FeatureBuilderAction(FeatureOrder.RemoveVrcGlobalsFromExpressionParams)]
        public void Apply() {
            paramz.GetRaw().parameters = paramz.GetRaw().parameters
                .Where(param => !FullControllerBuilder.VRChatGlobalParams.Contains(param.name))
                .ToArray();
        }
    }
}
