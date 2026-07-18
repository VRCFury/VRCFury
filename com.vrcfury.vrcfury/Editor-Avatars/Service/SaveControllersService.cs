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

        [FeatureBuilderAction(FeatureOrder.SaveControllers)]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                var raw = controller.Save(avatarObject);
                raw.parameters = raw.parameters
                    .OrderBy(p => p.name)
                    .ToArray();
                VRCAvatarUtils.SetAvatarController(avatar, controller.GetType(), raw);
            }
        }
    }
}
