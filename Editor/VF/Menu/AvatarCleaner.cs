using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Menu {
    public class AvatarCleaner {
        public static List<string> Cleanup(
            GameObject avatarObj,
            bool perform = false,
            Func<GameObject, bool> ShouldRemoveObj = null,
            Func<Object, bool> ShouldRemoveAsset = null,
            Func<string, bool> ShouldRemoveLayer = null,
            Func<string, bool> ShouldRemoveParam = null
        ) {
            var removeItems = new List<string>();

            if (ShouldRemoveObj != null) {
                var checkStack = new Stack<Transform>();
                checkStack.Push(avatarObj.transform);
                while (checkStack.Count > 0) {
                    var t = checkStack.Pop();
                    var obj = t.gameObject;

                    if (ShouldRemoveObj(obj) && (!PrefabUtility.IsPartOfPrefabInstance(obj) ||
                                                 PrefabUtility.IsOutermostPrefabInstanceRoot(obj))) {
                        removeItems.Add("Object: " + obj.name);
                        if (perform) Object.DestroyImmediate(obj);
                    } else {
                        foreach (Transform t2 in t) checkStack.Push(t2);
                    }
                }
            }

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            var avatarFx = VRCAvatarUtils.GetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX);
            if (avatarFx != null) {
                if (ShouldRemoveAsset != null && ShouldRemoveAsset(avatarFx)) {
                    removeItems.Add("Avatar descriptor FX layer setting");
                    if (perform) VRCAvatarUtils.SetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX, null);
                } else {
                    var vfac = new VFAController(avatarFx, VRCAvatarDescriptor.AnimLayerType.FX);
                    for (var i = 0; i < avatarFx.layers.Length; i++) {
                        var layer = avatarFx.layers[i];
                        if (ShouldRemoveLayer != null && ShouldRemoveLayer(layer.name)) {
                            removeItems.Add("Layer: " + layer.name);
                            if (perform) {
                                vfac.RemoveLayer(i);
                                i--;
                            }
                        }
                    }
                    for (var i = 0; i < avatarFx.parameters.Length; i++) {
                        var prm = avatarFx.parameters[i];
                        if (ShouldRemoveParam != null && ShouldRemoveParam(prm.name)) {
                            removeItems.Add("Parameter: " + prm.name);
                            if (perform) {
                                avatarFx.RemoveParameter(i);
                                i--;
                            }
                        }
                    }
                }
            }

            var syncParams = VRCAvatarUtils.GetAvatarParams(avatar);
            if (syncParams != null) {
                if (ShouldRemoveAsset != null && ShouldRemoveAsset(syncParams)) {
                    removeItems.Add("Avatar descriptor params setting");
                    if (perform) VRCAvatarUtils.SetAvatarParams(avatar, null);
                } else {
                    var prms = new List<VRCExpressionParameters.Parameter>(syncParams.parameters);
                    for (var i = 0; i < prms.Count; i++) {
                        if (ShouldRemoveParam != null && ShouldRemoveParam(prms[i].name)) {
                            removeItems.Add("Synced param: " + prms[i].name);
                            if (perform) {
                                prms.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                    if (perform) syncParams.parameters = prms.ToArray();
                }
            }

            void CheckMenu(VRCExpressionsMenu menu) {
                for (var i = 0; i < menu.controls.Count; i++) {
                    if (menu.controls[i].type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    if (menu.controls[i].subMenu == null) continue;
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(menu.controls[i].subMenu)) {
                        removeItems.Add("Menu Item: " + menu.controls[i].name);
                        if (perform) {
                            menu.controls.RemoveAt(i);
                            i--;
                        }
                    } else {
                        CheckMenu(menu.controls[i].subMenu);
                    }
                }
            }

            var m = VRCAvatarUtils.GetAvatarMenu(avatar);
            if (m != null) {
                if (ShouldRemoveAsset != null && ShouldRemoveAsset(m)) {
                    removeItems.Add("Avatar descriptor menu setting");
                    if (perform) VRCAvatarUtils.SetAvatarMenu(avatar, null);
                } else {
                    CheckMenu(m);
                }
            }

            return removeItems;
        }
    }
}