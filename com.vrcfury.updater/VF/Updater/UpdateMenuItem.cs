using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VF.Updater {
    public class UpdateMenuItem {
        private const string menu_name = "Tools/VRCFury/Update VRCFury";
        private const int menu_priority = 1000;

        [MenuItem(menu_name, priority = menu_priority)]
        public static void Upgrade() {
            Task.Run(() => AsyncUtils.ErrorDialogBoundary(UpgradeUnsafe));
        }

        private static async Task UpgradeUnsafe() {
            var actions = new PackageActions(msg => Debug.Log($"VRCFury Menu Updater: {msg}"));
            await VRCFuryUpdater.AddUpdateActions(false, actions);

            if (!actions.NeedsRun()) {
                await AsyncUtils.InMainThread(EditorUtility.ClearProgressBar);
                await AsyncUtils.DisplayDialog("No new updates are available.");
                return;
            }
            
            actions.CreateDirectory(await Markers.ManualUpdateInProgressMarker());

            await actions.Run();
        }
    }
}
