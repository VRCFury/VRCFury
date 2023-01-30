using System;
using System.Collections.Generic;
using System.Linq;
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
            Func<Component, bool> ShouldRemoveComponent = null,
            Func<Object, bool> ShouldRemoveAsset = null,
            Func<string, bool> ShouldRemoveLayer = null,
            Func<string, bool> ShouldRemoveParam = null
        ) {
            var removeItems = new List<string>();
            
            string GetPath(GameObject obj) {
                return AnimationUtility.CalculateTransformPath(obj.transform, avatarObj.transform);
            }

            if (ShouldRemoveObj != null || ShouldRemoveComponent != null) {
                var checkStack = new Stack<Transform>();
                checkStack.Push(avatarObj.transform);
                while (checkStack.Count > 0) {
                    var t = checkStack.Pop();
                    var obj = t.gameObject;

                    if (ShouldRemoveObj != null && ShouldRemoveObj(obj)) {
                        removeItems.Add("Object: " + GetPath(obj));
                        if (perform) RemoveObject(obj);
                    } else {
                        if (ShouldRemoveComponent != null) {
                            foreach (var component in obj.GetComponents<Component>()) {
                                if (!(component is Transform) && ShouldRemoveComponent(component)) {
                                    removeItems.Add("Component: " + component.GetType().Name + " on " + GetPath(obj));
                                    if (perform) RemoveComponent(component);
                                }
                            }
                        }
                        foreach (Transform t2 in t) checkStack.Push(t2);
                    }
                }
            }

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                foreach (var (controller, set, type) in VRCAvatarUtils.GetAllControllers(avatar)) {
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(controller)) {
                        removeItems.Add("Avatar descriptor " + VRCFEnumUtils.GetName(type) + " playable layer");
                        if (perform) set(null);
                    } else {
                        var vfac = new VFAController(controller, type);
                        for (var i = 0; i < controller.layers.Length; i++) {
                            var layer = controller.layers[i];
                            if (ShouldRemoveLayer != null && ShouldRemoveLayer(layer.name)) {
                                removeItems.Add("Layer: " + layer.name);
                                if (perform) {
                                    vfac.RemoveLayer(i);
                                    i--;
                                }
                            }
                        }
                        /*
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
                        */
                        if (perform) EditorUtility.SetDirty(controller);
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

                        if (perform) {
                            syncParams.parameters = prms.ToArray();
                            EditorUtility.SetDirty(syncParams);
                        }
                    }
                }

                void CheckMenu(VRCExpressionsMenu menu) {
                    for (var i = 0; i < menu.controls.Count; i++) {
                        var shouldRemove =
                            menu.controls[i].type == VRCExpressionsMenu.Control.ControlType.SubMenu
                            && menu.controls[i].subMenu
                            && ShouldRemoveAsset != null
                            && ShouldRemoveAsset(menu.controls[i].subMenu);
                        shouldRemove |=
                            menu.controls[i].type == VRCExpressionsMenu.Control.ControlType.Toggle
                            && menu.controls[i].parameter != null
                            && ShouldRemoveParam != null
                            && ShouldRemoveParam(menu.controls[i].parameter.name);
                        if (shouldRemove) {
                            removeItems.Add("Menu Item: " + menu.controls[i].name);
                            if (perform) {
                                menu.controls.RemoveAt(i);
                                i--;
                                EditorUtility.SetDirty(menu);
                            }
                        } else if (menu.controls[i].subMenu) {
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
            }

            return removeItems;
        }
        
        public static void RemoveComponent(Component c) {
            if (c.gameObject.GetComponents<Component>().Length == 2 && c.gameObject.transform.childCount == 0)
                RemoveObject(c.gameObject);
            else
                Object.DestroyImmediate(c);
        }
        public static void RemoveObject(GameObject obj) {
            if (!PrefabUtility.IsPartOfPrefabInstance(obj) || PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) {
                Object.DestroyImmediate(obj);
            } else {
                foreach (var component in obj.GetComponentsInChildren<Component>(true)) {
                    if (!(component is Transform)) {
                        Object.DestroyImmediate(component);
                    }
                }
                obj.name = "_deleted_" + obj.name;
            }
        }
    }
}