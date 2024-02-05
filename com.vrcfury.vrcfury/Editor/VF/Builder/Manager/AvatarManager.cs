using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Injector;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    [VFService]
    public class AvatarManager {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly LayerSourceService layerSourceService;

        private VFGameObject avatarObject => globals.avatarObject;
        private VRCAvatarDescriptor avatar => avatarObject.GetComponent<VRCAvatarDescriptor>();
        public string tmpDir => globals.tmpDir;

        private MenuManager _menu;
        public MenuManager GetMenu() {
            if (_menu == null) {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                var initializing = true;
                _menu = new MenuManager(menu, () => initializing ? 0 : globals.currentMenuSortPosition());

                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) _menu.MergeMenu(origMenu);
                
                VRCAvatarUtils.SetAvatarMenu(avatar, menu);
                initializing = false;
            }
            return _menu;
        }

        private readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager> _controllers
            = new Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>();
        public ControllerManager GetController(VRCAvatarDescriptor.AnimLayerType type) {
            if (!_controllers.TryGetValue(type, out var output)) {
                var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);
                VFController ctrl = null;
                if (existingController != null) ctrl = VFController.CopyAndLoadController(existingController, type);
                if (ctrl == null) ctrl = new AnimatorController();
                output = new ControllerManager(
                    ctrl,
                    GetParams,
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
                VRCAvatarUtils.SetAvatarController(avatar, type, ctrl);
            }
            return output;
        }
        public ControllerManager GetFx() {
            return GetController(VRCAvatarDescriptor.AnimLayerType.FX);
        }
        public IEnumerable<ControllerManager> GetAllUsedControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.controller != null)
                .Select(c => GetController(c.type))
                .ToArray();
        }

        private ParamManager _params;
        public ParamManager GetParams() {
            if (_params == null) {
                var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
                VRCExpressionParameters prms;
                if (origParams != null) {
                    prms = MutableManager.CopyRecursive(origParams);
                } else {
                    prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    prms.parameters = new VRCExpressionParameters.Parameter[]{};
                }
                VRCAvatarUtils.SetAvatarParams(avatar, prms);
                _params = new ParamManager(prms);
            }
            return _params;
        }

        public string GetCurrentlyExecutingFeatureName() {
            return globals.currentFeatureNameProvider();
        }

        public VFGameObject AvatarObject => avatarObject;
        public VRCAvatarDescriptor Avatar => avatar;
        public VFGameObject CurrentComponentObject => globals.currentComponentObject();

        public bool IsParamUsed(string name) {
            if (GetParams().GetRaw().FindParameter(name) != null) return true;
            foreach (var c in GetAllUsedControllers()) {
                if (c.GetRaw().GetParam(name) != null) return true;
            }
            return false;
        }
        public string MakeUniqueParamName(string name) {
            name = "VF" + globals.currentFeatureNumProvider() + "_" + name;

            int offset = 1;
            while (true) {
                var attempt = name + ((offset == 1) ? "" : offset+"");
                if (!IsParamUsed(attempt)) return attempt;
                offset++;
            }
        }
    }
}
