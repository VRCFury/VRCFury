using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class SaveControllersService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly TmpDirService tmpDirService;

        [FeatureBuilderAction(FeatureOrder.SaveControllers)]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                controller.parameters = controller.parameters
                    .OrderBy(p => p.name)
                    .ToArray();
                var raw = controller.Save(
                    avatarObject,
                    tmpDirService.GetTempDir(),
                    $"VRCFury {controller.GetType().ToString()}"
                );
                VRCAvatarUtils.SetAvatarController(avatar, controller.GetType(), raw);
            }
        }
    }
}
