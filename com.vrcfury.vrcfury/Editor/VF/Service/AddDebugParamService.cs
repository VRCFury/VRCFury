using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class AddDebugParamService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParamsService paramz;
        [VFAutowired] private readonly FixWriteDefaultsService fixWriteDefaultsService;
        
        [FeatureBuilderAction(FeatureOrder.AddDebugVrcParameter)]
        public void Apply() {
            if (!VRCExpressionParameterExtensions.SupportsUnsynced()) {
                return;
            }

            var parts = VrcfDebugLine.GetParts(avatarObject, fixWriteDefaultsService.IsStillBroken());
            var newParams = parts.Select(part => {
                var param = new VRCExpressionParameters.Parameter();
                param.name = "VF " + part;
                param.valueType = VRCExpressionParameters.ValueType.Bool;
                param.saved = false;
                param.defaultValue = 0;
                param.SetNetworkSynced(false);
                return param;
            });

            var raw = paramz.GetParams().GetRaw();
            raw.parameters = newParams.Concat(raw.parameters).ToArray();
        }
    }
}
