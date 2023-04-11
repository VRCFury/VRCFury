using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VF.Updater {
    public class UpdateMenuItem {
        private const string updateName = "Tools/VRCFury/Update VRCFury";
        private const int updatePriority = 1000;
        private const string removeName = "Tools/VRCFury/Uninstall VRCFury";
        private const int removePriority = 1001;

        [MenuItem(updateName, priority = updatePriority)]
        public static void Upgrade() {
            Task.Run(() => AsyncUtils.ErrorDialogBoundary(async () => {
                var actions = new PackageActions(msg => Debug.Log($"VRCFury Menu Updater: {msg}"));
                await VRCFuryUpdater.AddUpdateActions(false, actions);

                if (!actions.NeedsRun()) {
                    await AsyncUtils.InMainThread(EditorUtility.ClearProgressBar);
                    await AsyncUtils.DisplayDialog("No new updates are available.");
                    return;
                }
            
                actions.CreateMarker(await Markers.ManualUpdateInProgress());

                await actions.Run();
            }));
        }
        
        [MenuItem(removeName, priority = removePriority)]
        public static void Remove() {
            Task.Run(() => AsyncUtils.ErrorDialogBoundary(async () => {
                var actions = new PackageActions(msg => Debug.Log($"VRCFury Remover: {msg}"));
                var list = await actions.ListInstalledPacakges();
                var removeIds = list
                    .Select(p => p.name)
                    .Where(name => name.StartsWith("com.vrcfury"))
                    .ToArray();
                if (removeIds.Length == 0) {
                    throw new Exception("VRCFury packages not found");
                }
                
                var doIt = await AsyncUtils.InMainThread(() => EditorUtility.DisplayDialog("VRCFury",
                    "Uninstall VRCFury? Beware that all VRCFury scripts in your avatar will break.\n\nThe following packages will be removed:\n" + string.Join("\n", removeIds),
                    "Uninstall",
                    "Cancel"));
                if (!doIt) return;

                foreach (var id in removeIds) {
                    actions.RemovePackage(id);
                }

                await actions.Run();
            }));
        }
    }
}
