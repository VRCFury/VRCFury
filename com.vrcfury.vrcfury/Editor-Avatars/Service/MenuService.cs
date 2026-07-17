using JetBrains.Annotations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class MenuService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        
        private MenuManager _menu;
        public MenuManager GetMenu() {
            if (_menu == null) {
                var menu = VrcfObjectFactory.Create<VRCExpressionsMenu>();
                var initializing = true;
                _menu = new MenuManager(menu, () => initializing ? 0 : globals.currentMenuSortPosition);

                var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (origMenu != null) _menu.MergeMenu(origMenu);
                
                VRCAvatarUtils.SetAvatarMenu(avatar, menu);
                initializing = false;
            }
            return _menu;
        }

        [CanBeNull]
        public VRCExpressionsMenu GetReadOnlyMenu() {
            return VRCAvatarUtils.GetAvatarMenu(avatar);
        }
    }
}
