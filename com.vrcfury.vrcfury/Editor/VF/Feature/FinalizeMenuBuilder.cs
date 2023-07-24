using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class FinalizeMenuBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeMenu)]
        public void Apply() {
            var menuSettings = allFeaturesInRun.OfType<OverrideMenuSettings>().FirstOrDefault();
            var menu = manager.GetMenu();
            menu.SortMenu();
            MenuSplitter.SplitMenus(menu.GetRaw(), menuSettings);

            menu.GetRaw().ForEachMenu(ForEachItem: (control, path) => {
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
