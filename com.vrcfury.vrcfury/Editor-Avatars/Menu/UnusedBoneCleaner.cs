using System.Collections.Generic;
using UnityEditor;
using VF.Builder;
using VF.Exceptions;
using VF.Hooks;
using VF.Service;
using VF.Utils;

namespace VF.Menu {
    internal static class UnusedBoneCleaner {
        [VFInit]
        private static void InitHierarchyMenu() {
            MenuUtils.AddAvatarHierarchyCommand("Nuke unused bones", obj => VRCFuryBuildContext.Run(() => Run(obj)));
        }

        private static void Run(VFGameObject avatarObj) {
            if (!DialogUtils.DisplayDialog(
                    "Unused Bone Cleanup",
                    "MAKE SURE YOU BACKED UP YOUR SELECTED OBJECT FIRST" +
                    "\n\nContinue?",
                    "Yes",
                    "Cancel"
                )) return;

            var effects = Clean(avatarObj, false);
            if (effects.Count == 0) {
                DialogUtils.DisplayDialog(
                    "Unused Bone Cleanup",
                    "No unused bones found on avatar",
                    "Ok"
                );
                return;
            }
            var doIt = DialogUtils.DisplayDialog(
                "Unused Bone Cleanup",
                "The following bones will be deleted from your avatar:\n" + effects.Join('\n') +
                "\n\nContinue?",
                "Yes, Delete them",
                "Cancel"
            );
            if (!doIt) return;
            Clean(avatarObj, true);
        }
        
        private static List<string> Clean(VFGameObject avatarObj, bool perform = false) {
            var used = ArmatureLinkService.GetUsageReasons(avatarObj.GetAvatarRoot());
            return VRCFuryInjectorBuilder.CreateForManualEditing(avatarObj).GetService<AvatarCleaner>().Cleanup(
                avatarObj,
                perform: perform,
                ShouldRemoveObj: obj => {
                    if (PrefabUtility.IsPartOfPrefabInstance(obj) && !PrefabUtility.IsOutermostPrefabInstanceRoot(obj))
                        return false;
                    if (used.ContainsKey(obj))
                        return false;
                    return true;
                }
            );
        }
    }
}
