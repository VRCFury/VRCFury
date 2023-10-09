using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Service {
    [VFService]
    public class SpsOptionsService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly GlobalsService globals;

        public SpsOptions GetOptions() {
            var opts = globals.allFeaturesInRun.OfType<SpsOptions>().FirstOrDefault();
            return opts ?? new SpsOptions();
        }

        public string GetMenuPath() {
            var path = GetOptions().menuPath;
            if (string.IsNullOrWhiteSpace(path)) {
                path = "SPS";
            }
            manager.GetMenu().GetSubmenu(path);

            var icon = GetOptions().menuIcon != null
                ? GetOptions().menuIcon.Get()
                : VRCFuryEditorUtils.GetResource<Texture2D>("sps_icon.png");
            manager.GetMenu().SetIcon(path, icon);
            return path;
        }

        public string GetOptionsPath() {
            if (manager.AvatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Length == 0) {
                return GetMenuPath();
            }
            var path = GetMenuPath() + "/<b>Options";
            manager.GetMenu().GetSubmenu(path);
            manager.GetMenu().SetIconGuid(path, "16e0846165acaa1429417e757c53ef9b");
            return path;
        }
    }
}
