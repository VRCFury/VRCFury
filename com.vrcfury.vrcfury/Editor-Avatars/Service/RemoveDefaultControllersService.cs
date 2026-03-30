using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class RemoveDefaultControllersService {
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        
        [FeatureBuilderAction(FeatureOrder.RemoveDefaultControllers)]
        public void Apply() {
            foreach (var c in VRCAvatarUtils.GetAllControllers(avatar)) {
                /*
                 * The default additive playable layer is a major contributor to the "3x unity blendshape" bug.
                 * Simply removing it whenever it's set to the default goes a long way to resolving the issue.
                 */
                if (c.type == VRCAvatarDescriptor.AnimLayerType.Additive && c.isDefault) {
                    c.set(null);
                }
                
                /*
                 * The default VRC action layer shouldn't be there unless the user's menu specifically uses VRCEmote.
                 * Otherwise it just takes up space when people are merging in GogoLoco.
                 */
                // Temporarily disabled because the default action layer still has AFK which some people use
                // if (c.type == VRCAvatarDescriptor.AnimLayerType.Action && c.isDefault) {
                //     if (!MenuUsesVrcEmote()) {
                //         c.set(null);
                //     }
                // }
            }
        }

        private bool MenuUsesVrcEmote() {
            var usesVrcEmote = false;
            menu.GetRaw().ForEachMenu(ForEachItem: (item,path) => {
                if (item?.parameter?.name == "VRCEmote") {
                    usesVrcEmote = true;
                }
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return usesVrcEmote;
        }
    }
}
