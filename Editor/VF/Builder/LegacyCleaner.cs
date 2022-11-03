using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
                PurgeFromAnimator(fx, VRCAvatarDescriptor.AnimLayerType.FX);
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
                PurgeFromParams(prms);
            }

            EditorUtility.SetDirty(avatar);
        }
        
        private static void PurgeFromMenu(VRCExpressionsMenu menu) {
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
        
        private static void PurgeFromParams(VRCExpressionParameters syncedParams) {
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
            syncedParamsList.RemoveAll(param => param.name.StartsWith("Senky") || param.name.StartsWith("VRCFury__"));
            syncedParams.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(syncedParams);
        }
        
        private static void PurgeFromAnimator(AnimatorController ctrl, VRCAvatarDescriptor.AnimLayerType type) {
            // Clean up layers
            var vfac = new VFAController(ctrl, type);
            for (var i = 0; i < ctrl.layers.Length; i++) {
                var layer = ctrl.layers[i];
                if (layer.name.StartsWith("[VRCFury]")) {
                    vfac.RemoveLayer(i);
                    i--;
                }
            }
            // Clean up parameters
            for (var i = 0; i < ctrl.parameters.Length; i++) {
                var param = ctrl.parameters[i];
                if (param.name.StartsWith("Senky") || param.name.StartsWith("VRCFury__")) {
                    ctrl.RemoveParameter(param);
                    i--;
                }
            }
        }
        
        private static bool IsVrcfAsset(Object obj) {
            return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
        }
    }
}