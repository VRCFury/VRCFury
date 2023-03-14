using System.Threading.Tasks;
using UnityEditor;

namespace VF.Updater {
    public class UpdateMenuItem {
        private const string menu_name = "Tools/VRCFury/Update VRCFury";
        private const int menu_priority = 1000;

        [MenuItem(menu_name, priority = menu_priority)]
        public static void Upgrade() {
            Task.Run(() => VRCFuryUpdater.UpdateAll());
        }
    }
}
