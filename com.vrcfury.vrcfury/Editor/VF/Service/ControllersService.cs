using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class ControllersService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly LayerSourceService layerSourceService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        private ParamManager paramz => paramsService.GetParams();

        private readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager> _controllers
            = new Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>();
        public ControllerManager GetController(VRCAvatarDescriptor.AnimLayerType type) {
            if (!_controllers.TryGetValue(type, out var output)) {
                var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);
                VFController ctrl = null;
                if (existingController != null) ctrl = VFController.CopyAndLoadController(existingController, type);
                if (ctrl == null) ctrl = new VFController(VrcfObjectFactory.Create<AnimatorController>());
                output = new ControllerManager(
                    ctrl,
                    () => paramz,
                    type,
                    globals.currentFeatureNumProvider,
                    globals.currentFeatureClipPrefixProvider,
                    MakeUniqueParamName,
                    layerSourceService
                );
                foreach (var layer in ctrl.GetLayers()) {
                    layerSourceService.SetSource(layer, isDefault ? LayerSourceService.VrcDefaultSource : LayerSourceService.AvatarDescriptorSource);
                }
                _controllers[type] = output;
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl.GetRaw());
            }
            return output;
        }
        public ControllerManager GetFx() {
            return GetController(VRCAvatarDescriptor.AnimLayerType.FX);
        }
        public IList<ControllerManager> GetAllMutatedControllers() {
            return _controllers.Values.ToArray();
        }
        public IList<ControllerManager> GetAllUsedControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.controller != null)
                .Select(c => GetController(c.type))
                .ToArray();
        }
        public IList<VFController> GetAllReadOnlyControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Select(found => found.controller as AnimatorController)
                .NotNull()
                .Select(c => new VFController(c))
                .ToArray();
        }
        
        private bool IsParamUsed(string name) {
            if (paramsService.GetReadOnlyParams()?.FindParameter(name) != null) return true;
            foreach (var c in GetAllReadOnlyControllers()) {
                if (c.GetParam(name) != null) return true;
            }
            return false;
        }
        public string MakeUniqueParamName(string originalName) {
            var name = "VF" + globals.currentFeatureNumProvider() + "_" + originalName;

            int offset = 1;
            while (true) {
                var attempt = name + ((offset == 1) ? "" : offset+"");
                if (!IsParamUsed(attempt)) {
                    parameterSourceService.RecordParamSource(
                        attempt,
                        globals.currentFeatureObjectPath(),
                        originalName
                    );
                    return attempt;
                }
                offset++;
            }
        }
    }
}
