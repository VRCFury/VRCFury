using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    [VFService]
    public class RemoveDefaultControllersService {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.RemoveDefaultControllers)]
        public void Apply() {
            var avatar = manager.Avatar;
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
                if (c.type == VRCAvatarDescriptor.AnimLayerType.Action && c.isDefault) {
                    if (!MenuUsesVrcEmote()) {
                        c.set(null);
                    }
                }
            }
        }

        private bool MenuUsesVrcEmote() {
            var usesVrcEmote = false;
            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (item,path) => {
                if (item?.parameter?.name == "VRCEmote") {
                    usesVrcEmote = true;
                }
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return usesVrcEmote;
        }
    }
}
