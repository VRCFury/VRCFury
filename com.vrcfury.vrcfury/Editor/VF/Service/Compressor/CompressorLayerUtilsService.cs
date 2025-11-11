using System.Linq;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    [VFService]
    internal class CompressorLayerUtilsService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ParamsService paramsService;
        
        public VRCExpressionParameters.Parameter MakeParam(string name, VRCExpressionParameters.ValueType type, bool synced) {
            var param = new VRCExpressionParameters.Parameter {
                name = controllers.MakeUniqueParamName(name),
                valueType = type
            };
            param.SetNetworkSynced(synced);
            paramsService.GetParams().GetRaw().Add(param);
            return param;
        }

        public void FixWd(VFLayer layer) {
            var wdoff = controllers.GetFx().GetLayers().SelectMany(l => l.allStates).Any(state => !state.writeDefaultValues);
            if (wdoff) {
                foreach (var state in layer.allStates) {
                    state.writeDefaultValues = false;
                }
            }
        }
    }
}
