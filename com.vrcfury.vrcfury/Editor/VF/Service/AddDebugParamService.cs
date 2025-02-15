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
        
        [FeatureBuilderAction(FeatureOrder.AddDebugVrcParameter)]
        public void Apply() {
            if (!VRCExpressionParameterExtensions.SupportsUnsynced()) {
                return;
            }

            var param = new VRCExpressionParameters.Parameter();
            param.name = "_VF " + VrcfDebugLine.GetOutputString(avatarObject);
            param.valueType = VRCExpressionParameters.ValueType.Bool;
            param.saved = false;
            param.defaultValue = 0;
            param.SetNetworkSynced(false);

            var raw = paramz.GetParams().GetRaw();
            raw.parameters = new[] { param }.Concat(raw.parameters).ToArray();
        }
    }
}
