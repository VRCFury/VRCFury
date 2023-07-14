using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Exceptions;

namespace VF.Menu {
    public class UnusedBoneCleaner {
        [MenuItem(MenuItems.unusedBones, priority = MenuItems.unusedBonesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                Run(MenuUtils.GetSelectedAvatar());
            });
        }
        [MenuItem(MenuItems.unusedBones, true)]
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
        
        private static List<string> Clean(VFGameObject avatarObj, bool perform = false) {
            var usedBones = new HashSet<Transform>();
            foreach (var s in avatarObj.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                foreach (var bone in s.bones) {
                    if (bone) usedBones.Add(bone);
                }
                if (s.rootBone) usedBones.Add(s.rootBone);
            }
            foreach (var c in avatarObj.GetComponentsInSelfAndChildren<IConstraint>()) {
                for (var i = 0; i < c.sourceCount; i++) {
                    var t = c.GetSource(i).sourceTransform;
                    if (t) usedBones.Add(t);
                }
            }
            return AvatarCleaner.Cleanup(
                avatarObj,
                perform: perform,
                ShouldRemoveObj: obj => {
                    var parent = obj.parent;
                    if (!parent) return false;
                    var name = obj.name;
                    var parentName = parent.name;
                    if (PrefabUtility.IsPartOfPrefabInstance(obj) && !PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) {
                        return false;
                    }
                    if (!name.Contains(parentName)) return false;
                    if (name == parentName + "_end") return false;
                    if (obj.transform.childCount > 0) return false;
                    if (obj.GetComponents<UnityEngine.Component>().Length > 1) return false;
                    if (usedBones.Contains(obj.transform)) return false;
                    return true;
                }
            );
        }
    }
}
