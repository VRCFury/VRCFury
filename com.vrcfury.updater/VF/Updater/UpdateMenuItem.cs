using System.Threading.Tasks;
using UnityEditor;

namespace VF.Updater {
    public class UpdateMenuItem {
        private const string header_name = "Tools/VRCFury/Update";
        private const int header_priority = 1000;
        private const string menu_name = "Tools/VRCFury/Update VRCFury";
        private const int menu_priority = 1001;

        [MenuItem(header_name, priority = header_priority)]
        private static void MarkerUpdate() {
        }

        [MenuItem(header_name, true)]
        private static bool MarkerUpdate2() {
            return false;
        }
        
        [MenuItem(menu_name, priority = menu_priority)]
        public static void Upgrade() {
            Task.Run(() => VRCFuryUpdater.UpdateAll());
        }
    }
}
