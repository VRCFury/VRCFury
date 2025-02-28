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
    internal class RemoveVrcGlobalsFromExpressionParamsService {
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();

        [FeatureBuilderAction(FeatureOrder.RemoveVrcGlobalsFromExpressionParams)]
        public void Apply() {
            foreach (var p in paramz.GetRaw().parameters) {
                if (FullControllerBuilder.VRChatGlobalParams.Contains(p.name)) {
                    p.SetNetworkSynced(false, true);
                }
            }
        }
    }
}
