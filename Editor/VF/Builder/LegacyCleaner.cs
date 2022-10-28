using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    public class LegacyCleaner {
        /** Removes VRCF from avatars made in the pre-"do not touch the avatar" days */
        public static void Clean(GameObject avatarObject) {
            var animator = avatarObject.GetComponent<Animator>();
            if (animator != null) {
                if (IsVrcfAsset(animator.runtimeAnimatorController)) {
                    animator.runtimeAnimatorController = null;
                }
            }

            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            var fx = VRCAvatarUtils.GetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX);
            if (IsVrcfAsset(fx)) {
                VRCAvatarUtils.SetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX, null);
            } else if (fx != null) {
                ControllerManager.PurgeFromAnimator(fx, VRCAvatarDescriptor.AnimLayerType.FX);
            }

            var menu = VRCAvatarUtils.GetAvatarMenu(avatar);
            if (IsVrcfAsset(menu)) {
                VRCAvatarUtils.SetAvatarMenu(avatar, null);
            } else if (menu != null) {
                MenuSplitter.JoinMenus(menu);
                PurgeFromMenu(menu);
                MenuSplitter.SplitMenus(menu);
            }

            var prms = VRCAvatarUtils.GetAvatarParams(avatar);
            if (IsVrcfAsset(prms)) {
                VRCAvatarUtils.SetAvatarParams(avatar, null);
            } else if (prms != null) {
                ParamManager.PurgeFromParams(prms);
            }

            EditorUtility.SetDirty(avatar);
        }
        
        public static void PurgeFromMenu(VRCExpressionsMenu menu) {
            if (menu == null) return;
            for (var i = 0; i < menu.controls.Count; i++) {
                var remove = false;
                var control = menu.controls[i];
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null) {
                    if (control.subMenu.name.StartsWith("VRCFury")) {
                        remove = true;
                    }
                    if (IsVrcfAsset(control.subMenu)) {
                        remove = true;
                    }
                }
                if (control.name == "SenkyFX" || control.name == "VRCFury") {
                    remove = true;
                }
                if (control.parameter != null && control.parameter.name != null && control.parameter.name.StartsWith("VRCFury")) {
                    remove = true;
                }
                if (control.subParameters != null && control.subParameters.Any(p => p != null && p.name.StartsWith("VRCFury"))) {
                    remove = true;
                }
                if (remove) {
                    menu.controls.RemoveAt(i);
                    i--;
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    PurgeFromMenu(control.subMenu);
                }
            }
        }
        
        public static bool IsVrcfAsset(Object obj) {
            return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
        }
    }
}