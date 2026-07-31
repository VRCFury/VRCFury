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
                valueType = type,
                saved = false
            };
            param.SetNetworkSynced(synced);
            paramsService.GetParams().GetRaw().Add(param);
            return param;
        }

        public VFAParam AddToController(ControllerManager controller, VRCExpressionParameters.Parameter param) {
            switch (param.valueType) {
                case VRCExpressionParameters.ValueType.Bool:
                    return controller.AddParam(new VFABool(param.name, param.defaultValue != 0));
                case VRCExpressionParameters.ValueType.Int:
                    return controller.AddParam(new VFAInteger(param.name, (int)param.defaultValue));
                case VRCExpressionParameters.ValueType.Float:
                    return controller.AddParam(new VFAFloat(param.name, param.defaultValue));
                default:
                    throw new System.ArgumentException("Unknown parameter type");
            }
        }

        public void FixWd(VFLayer layer, ControllerManager controller) {
            var wdoff = controller.GetLayers().SelectMany(l => l.allStates).Any(state => !state.writeDefaultValues);
            if (wdoff) {
                foreach (var state in layer.allStates) {
                    state.writeDefaultValues = false;
                }
            }
        }
    }
}
