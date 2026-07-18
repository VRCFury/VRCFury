using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using UnityEditor.Animations;
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
            return _controllers.GetOrCreate(type, () => MakeController(type));
        }

        private ControllerManager MakeController(VRCAvatarDescriptor.AnimLayerType type) {
            var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);

            VFController ctrl = null;
            if (existingController != null) {
                ctrl = VFControllerWithVrcType.Load(
                    existingController,
                    type,
                    new VFLoadContext {
                        OwnerObject = globals.avatarObject,
                        AnimatorObject = globals.avatarObject,
                        RootBindingsApplyToAvatar = true
                    }
                );
            }
            if (ctrl == null) {
                ctrl = VFController.Create();
            }
            var output = new ControllerManager(
                ctrl,
                () => paramz,
                type,
                () => globals.currentFeatureNum,
                () => globals.currentFeatureClipPrefix,
                MakeUniqueParamName,
                layerSourceService
            );
            if (existingController != null) {
                foreach (var layer in output.GetLayers()) {
                    layerSourceService.SetSource(layer,
                        isDefault ? LayerSourceService.VrcDefaultSource : LayerSourceService.AvatarDescriptorSource);
                }
            }
            return output;
        }

        public void ClearCache() {
            _controllers.Clear();
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
                .Concat(_controllers.Values)
                .Distinct()
                .ToArray();
        }

        private bool IsParamUsed(string name) {
            if (paramsService.GetReadOnlyParams()?.FindParameter(name) != null) return true;
            foreach (var c in GetAllUsedControllers()) {
                if (c.GetParam(name) != null) return true;
            }
            return false;
        }
        public string MakeUniqueParamName(string originalName) {
            var name = "VF" + globals.currentFeatureNum + "_" + originalName;

            int offset = 1;
            while (true) {
                var attempt = name + ((offset == 1) ? "" : offset+"");
                if (!IsParamUsed(attempt)) {
                    parameterSourceService.RecordParamSource(
                        attempt,
                        globals.currentFeatureObjectPath,
                        originalName
                    );
                    return attempt;
                }
                offset++;
            }
        }
    }
}
