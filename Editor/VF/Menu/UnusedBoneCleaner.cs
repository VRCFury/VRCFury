using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder.Exceptions;

namespace VF.Menu {
    public class UnusedBoneCleaner {
        [MenuItem(MenuItems.unusedBones_name, priority = MenuItems.unusedBones_priority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                Run(MenuUtils.GetSelectedAvatar());
            });
        }
        [MenuItem(MenuItems.unusedBones_name, true)]
        private static bool Check() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
        
        private static void Run(GameObject avatarObj) {
            var effects = Clean(avatarObj, false);
            if (effects.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Unused Bone Cleanup",
                    "No unused bones found on avatar",
                    "Ok"
                );
                return;
            }
            var doIt = EditorUtility.DisplayDialog(
                "Unused Bone Cleanup",
                "The following bones will be deleted from your avatar:\n" + string.Join("\n", effects) +
                "\n\nContinue?",
                "Yes, Delete them",
                "Cancel"
            );
            if (!doIt) return;
            Clean(avatarObj, true);
        }
        
        private static List<string> Clean(GameObject avatarObj, bool perform = false) {
            var usedBones = new HashSet<Transform>();
            foreach (var s in avatarObj.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                foreach (var bone in s.bones) {
                    if (bone) usedBones.Add(bone);
                }
                if (s.rootBone) usedBones.Add(s.rootBone);
            }
            foreach (var c in avatarObj.GetComponentsInChildren<IConstraint>(true)) {
                for (var i = 0; i < c.sourceCount; i++) {
                    var t = c.GetSource(i).sourceTransform;
                    if (t) usedBones.Add(t);
                }
            }
            return AvatarCleaner.Cleanup(
                avatarObj,
                perform: perform,
                ShouldRemoveObj: obj => {
                    var parent = obj.transform.parent;
                    if (!parent) return false;
                    if (!obj.name.Contains(parent.name)) return false;
                    if (obj.name == parent.name + "_end") return false;
                    if (obj.transform.childCount > 0) return false;
                    if (obj.GetComponents<Component>().Length > 1) return false;
                    if (usedBones.Contains(obj.transform)) return false;
                    return true;
                }
            );
        }
    }
}
