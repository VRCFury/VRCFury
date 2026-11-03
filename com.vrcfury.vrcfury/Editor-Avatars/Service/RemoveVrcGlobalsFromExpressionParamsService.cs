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
        [VFAutowired] private readonly ExpressionParamsService expressionParams;

        [FeatureBuilderAction(FeatureOrder.RemoveVrcGlobalsFromExpressionParams)]
        public void Apply() {
            if (!VRCExpressionParameterExtensions.SupportsUnsynced()) return;
            foreach (var p in expressionParams) {
                if (FullControllerBuilder.VRChatGlobalParams.Contains(p.name)) {
                    p.Unsynced();
                }
            }
        }
    }
}
