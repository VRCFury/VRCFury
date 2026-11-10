using System;
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
        [VFAutowired] private readonly VRCFObjectPathCache objectPaths;
        [VFAutowired] private readonly VRCFuryInjector injector;
        private readonly Lazy<UniqueParamService> uniqueParams;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;

        private readonly Dictionary<VRCAvatarDescriptor.AnimLayerType, VFAvatarController> _controllers
            = new Dictionary<VRCAvatarDescriptor.AnimLayerType, VFAvatarController>();
        private bool applyBaseMask = true;
        private bool controllerLoadingAllowed;
        private readonly Dictionary<VFAvatarController, (HashSet<VFLayer> layers, HashSet<string> parameters)> originals
            = new Dictionary<VFAvatarController, (HashSet<VFLayer>, HashSet<string>)>();

        public ControllersService() {
            // The allocator also depends on this service, so resolve it only when allocating a name.
            uniqueParams = new Lazy<UniqueParamService>(() => injector.GetService<UniqueParamService>());
        }

        public VFAvatarController GetController(VRCAvatarDescriptor.AnimLayerType type) {
            return _controllers.GetOrCreate(type, () => MakeController(type));
        }

        private VFAvatarController MakeController(VRCAvatarDescriptor.AnimLayerType type) {
            if (!globals.useOriginalAssets && applyBaseMask && !controllerLoadingAllowed) {
                throw new System.InvalidOperationException(
                    "A playable-layer controller was loaded before the Load All Controllers build phase."
                );
            }

            var (isDefault, existingController) = VRCAvatarUtils.GetAvatarController(avatar, type);

            VFController ctrl = null;
            if (existingController != null) {
                var context = new VFLoadContext {
                    OwnerObject = globals.avatarObject,
                    AnimatorObject = globals.avatarObject,
                    RootBindingsApplyToAvatar = true,
                    ObjectPaths = objectPaths,
                    ReverseObjectPaths = true
                };
                ctrl = applyBaseMask && !globals.useOriginalAssets
                    ? VFControllerWithVrcType.Load(existingController, type, context)
                    : VFController.Load(existingController, context);
            }
            if (ctrl == null) {
                ctrl = VFController.Create();
            }
            var output = new VFAvatarController(
                ctrl,
                type,
                () => globals.currentFeatureNum,
                () => globals.currentFeatureClipPrefix,
                name => uniqueParams.Value.GetUniqueName(name),
                layerSourceService,
                expressionParams
            );
            if (existingController != null) {
                foreach (var layer in output.GetLayers()) {
                    layerSourceService.SetSource(layer,
                        isDefault ? LayerSourceService.VrcDefaultSource : LayerSourceService.AvatarDescriptorSource);
                }
            }
            if (globals.useOriginalAssets) {
                originals[output] = (new HashSet<VFLayer>(output.GetLayers()), new HashSet<string>(output.parameters.Select(p => p.name)));
            }
            return output;
        }

        public void ClearCache(bool applyBaseMask = true) {
            this.applyBaseMask = applyBaseMask;
            _controllers.Clear();
            originals.Clear();
        }

        public void LoadAllControllers() {
            controllerLoadingAllowed = true;
            GetAllUsedControllers();
        }

        public VFAvatarController GetFx() {
            return GetController(VRCAvatarDescriptor.AnimLayerType.FX);
        }
        public VFAvatarController GetAction() {
            return GetController(VRCAvatarDescriptor.AnimLayerType.Action);
        }
        public IList<VFAvatarController> GetAllMutatedControllers() {
            return _controllers.Values.ToArray();
        }
        public IList<VFAvatarController> GetAllUsedControllers() {
            return VRCAvatarUtils.GetAllControllers(avatar)
                .Where(c => c.controller != null)
                .Select(c => GetController(c.type))
                .Concat(_controllers.Values)
                .Distinct()
                .ToArray();
        }

        public void CommitRemovals() {
            if (!globals.useOriginalAssets) return;
            foreach (var pair in originals) {
                var controller = pair.Key;
                var source = controller.GetSourceAsset() as AnimatorController;
                if (source == null) continue;
                var removedLayers = new HashSet<string>(pair.Value.layers.Except(controller.GetLayers())
                    .Select(layer => layer.name));
                var removedParams = new HashSet<string>(pair.Value.parameters.Except(controller.parameters.Select(p => p.name)));
                if (removedLayers.Count == 0 && removedParams.Count == 0) continue;
                // Cleanup only removes layers/declarations. Preserve all authored data on the surviving entries,
                // rather than running build finalization (which normalizes types, defaults, and ordering).
                source.layers = source.layers.Where(layer => !removedLayers.Contains(layer.name)).ToArray();
                source.parameters = source.parameters.Where(parameter => !removedParams.Contains(parameter.name)).ToArray();
                source.Dirty();
            }
        }

    }
}
