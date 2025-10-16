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
            return _controllers.GetOrCreate(type, () => MakeController(type));
        }

        private ControllerManager MakeController(VRCAvatarDescriptor.AnimLayerType type) {
            var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);
            
            VFController ctrl = null;
            if (existingController is AnimatorController eac && VrcfObjectFactory.DidCreate(eac)) {
                // We probably made this in an earlier preprocessor hook, so we can just adopt it
                // WARNING - THIS DOES NOT CLONE ANIMATION CLIPS PROPERLY BECAUSE THEY WERE ALREADY SET BACK TO THE ORIGINALS
                // It is unsafe to mess with animation clips after the first preprocessor hook!!
                ctrl = new VFController(eac);
            } else {
                if (existingController != null) ctrl = VFController.CopyAndLoadController(existingController, type);
                if (ctrl == null) ctrl = new VFController(VrcfObjectFactory.Create<AnimatorController>());
                foreach (var layer in ctrl.GetLayers()) {
                    layerSourceService.SetSource(layer,
                        isDefault ? LayerSourceService.VrcDefaultSource : LayerSourceService.AvatarDescriptorSource);
                }
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl.GetRaw());
            }
            return new ControllerManager(
                ctrl,
                () => paramz,
                type,
                () => globals.currentFeatureNum,
                () => globals.currentFeatureClipPrefix,
                MakeUniqueParamName,
                layerSourceService
            );
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
