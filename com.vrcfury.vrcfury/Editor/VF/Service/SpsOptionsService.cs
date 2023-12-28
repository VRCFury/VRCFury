using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Service {
    [VFService]
    public class SpsOptionsService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly MenuChangesService menuChanges;

        [FeatureBuilderAction(FeatureOrder.MoveSpsMenus)]
        public void MoveMenus() {
            var mainPath = GetMenuPath();
            
            menuChanges.AddExtraAction(new MoveMenuItem {
                fromPath = "Holes",
                toPath = mainPath
            });
            menuChanges.AddExtraAction(new MoveMenuItem {
                fromPath = "Sockets",
                toPath = mainPath
            });
            if (mainPath != "SPS") {
                menuChanges.AddExtraAction(new MoveMenuItem {
                    fromPath = "SPS",
                    toPath = mainPath
                });
            }

            var icon = GetOptions().menuIcon?.Get()
                       ?? VRCFuryEditorUtils.GetResource<Texture2D>("sps_icon.png");
            menuChanges.AddExtraAction(new SetIcon {
                path = mainPath,
                icon = icon
            });

            var optionsPath = GetOptionsPath();
            if (optionsPath != mainPath) {
                var optionsIcon = VRCFuryEditorUtils.LoadGuid<Texture2D>("16e0846165acaa1429417e757c53ef9b");
                if (optionsIcon != null) {
                    menuChanges.AddExtraAction(new SetIcon {
                        path = optionsPath,
                        icon = optionsIcon
                    });
                }
            }
        }

        public SpsOptions GetOptions() {
            var opts = globals.allFeaturesInRun.OfType<SpsOptions>().FirstOrDefault();
            return opts ?? new SpsOptions();
        }

        public string GetMenuPath() {
            var path = GetOptions().menuPath;
            if (string.IsNullOrWhiteSpace(path)) {
                path = "SPS";
            }
            return path;
        }

        public string GetOptionsPath() {
            if (manager.AvatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Length == 0) {
                return GetMenuPath();
            }
            return GetMenuPath() + "/<b>Options";
        }
    }
}
