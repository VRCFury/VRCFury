using UnityEditor;
using VF.Component;
using VF.Utils;

namespace VF.Menu {
    internal static class ReserializeAllMenuItem {
        [MenuItem(MenuItems.reserialize, priority = MenuItems.reserializePriority)]
        private static void Reserialize() {
            VRCFuryBuildContext.Run(() => {
                var doIt = DialogUtils.DisplayDialog(
                    "VRCFury",
                    "This is intended for VRCFury developers only, in order to quickly" +
                    " refresh serialization of all VRCF components in a project." +
                    "\n\n" +
                    "THIS WILL LOAD ALL SCENES AND TAKE A LONG TIME!" +
                    "\n\n" +
                    "Continue?",
                    "Ok",
                    "Cancel"
                );
                if (!doIt) return;

                BulkUpgradeUtils.WithAllScenesOpen(() => {
                    foreach (var vrcf in BulkUpgradeUtils.FindAll<VRCFuryComponent>()) {
                        vrcf.Upgrade();
                    }
                });

                DialogUtils.DisplayDialog(
                    "VRCFury",
                    $"Done",
                    "Ok"
                );
            });
        }
    }
}
