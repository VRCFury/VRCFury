using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class FinalizeMenuService {
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction(FeatureOrder.FinalizeMenu)]
        public void Apply() {
            var menuSettings = globals.allFeaturesInRun.OfType<OverrideMenuSettings>().FirstOrDefault();
            menu.SortMenu();

            foreach (var c in globals.allFeaturesInRun.OfType<ReorderMenuItem>()) {
                menu.Reorder(c.path, c.position);
            }
            
            MenuSplitter.SplitMenus(menu.GetRaw(), menuSettings);

            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                // Menu items with invalid types say "Button" in the editor, but do nothing in gesture manager
                // and in game, and cause av3emu to throw an exception
                if (!VRCFEnumUtils.IsValid(control.type)) {
                    control.type = VRCExpressionsMenu.Control.ControlType.Button;
                }

                // VRChat doesn't care, but SDK3ToCCKConverter crashes if there are any null parameters
                // on a submenu. GestureManager crashes if there's any null parameters on ANYTHING.
                if (control.parameter == null) {
                    control.parameter = new VRCExpressionsMenu.Control.Parameter() {
                        name = ""
                    };
                }

                // Av3emulator crashes if subParameters is null
                if (control.subParameters == null) {
                    control.subParameters = new VRCExpressionsMenu.Control.Parameter[] { };
                }
                
                // The build will include assets and things from the linked submenu, even if the control
                // has been changed to something that isn't a submenu
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    control.subMenu = null;
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
        }
    }
}
