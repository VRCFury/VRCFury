using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Menu {
    public class AvatarCleaner {
        public static List<string> Cleanup(
            VFGameObject avatarObj,
            bool perform = false,
            Func<VFGameObject, bool> ShouldRemoveObj = null,
            Func<UnityEngine.Component, bool> ShouldRemoveComponent = null,
            Func<Object, bool> ShouldRemoveAsset = null,
            Func<string, bool> ShouldRemoveLayer = null,
            Func<string, bool> ShouldRemoveParam = null
        ) {
            var removeItems = new List<string>();
            
            string GetPath(GameObject obj) {
                return AnimationUtility.CalculateTransformPath(obj.transform, avatarObj.transform);
            }

            if (ShouldRemoveAsset != null) {
                var animators = avatarObj.GetComponentsInSelfAndChildren<Animator>();
                foreach (var animator in animators) {
                    if (animator.runtimeAnimatorController != null &&
                        ShouldRemoveAsset(animator.runtimeAnimatorController)) {
                        removeItems.Add("Animator Controller at path " + GetPath(animator.gameObject));
                        if (perform) {
                            animator.runtimeAnimatorController = null;
                            VRCFuryEditorUtils.MarkDirty(animator);
                        }
                    }
                }
            }

            if (ShouldRemoveObj != null || ShouldRemoveComponent != null) {
                var checkStack = new Stack<Transform>();
                checkStack.Push(avatarObj.transform);
                while (checkStack.Count > 0) {
                    var t = checkStack.Pop();
                    var obj = t.gameObject;

                    // TODO: ShouldRemoveObj should maybe verify if anything else in the avatar is using the object
                    // (constraints and things)
                    
                    var removeObject = ShouldRemoveObj != null && ShouldRemoveObj(obj);
                    if (ShouldRemoveComponent != null && obj.gameObject.transform.childCount == 0) {
                        var allComponents = obj.GetComponents<UnityEngine.Component>()
                            .Where(c => c != null && !(c is Transform))
                            .ToArray();
                        if (allComponents.Length > 0) {
                            var allComponentsAreRemoved = allComponents.All(ShouldRemoveComponent);
                            removeObject |= allComponentsAreRemoved;
                        }
                    }

                    if (removeObject) {
                        removeItems.Add("Object: " + GetPath(obj));
                        if (perform) RemoveObject(obj);
                    } else {
                        if (ShouldRemoveComponent != null) {
                            foreach (var component in obj.GetComponents<UnityEngine.Component>()) {
                                if (component != null && !(component is Transform) && ShouldRemoveComponent(component)) {
                                    removeItems.Add(component.GetType().Name + " on " + GetPath(obj));
                                    if (perform) RemoveComponent(component);
                                }
                            }
                        }

                        // Make sure RemoveComponent didn't remove this object!
                        if (t) {
                            foreach (Transform t2 in t) checkStack.Push(t2);
                        }
                    }
                }
            }

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                foreach (var (controller, set, type) in VRCAvatarUtils.GetAllControllers(avatar)) {
                    if (controller == null) continue;
                    var typeName = VRCFEnumUtils.GetName(type);
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(controller)) {
                        removeItems.Add("Avatar Controller: " + typeName);
                        if (perform) set(null);
                    } else {
                        var vfac = new VFAController(controller, type);
                        var removedLayers = new HashSet<AnimatorStateMachine>();
                        if (ShouldRemoveLayer != null) {
                            for (var i = 0; i < controller.layers.Length; i++) {
                                var layer = controller.layers[i];
                                if (!ShouldRemoveLayer(layer.name)) continue;
                                removeItems.Add(typeName + " Layer: " + layer.name);
                                removedLayers.Add(layer.stateMachine);
                                if (perform) {
                                    vfac.RemoveLayer(i);
                                    i--;
                                }
                            }
                        }

                        if (ShouldRemoveParam != null) {
                            for (var i = 0; i < controller.parameters.Length; i++) {
                                var prm = controller.parameters[i].name;
                                if (!ShouldRemoveParam(prm)) continue;

                                var prmUsed = controller.layers
                                    .Where(layer => !removedLayers.Contains(layer.stateMachine))
                                    .Any(layer => IsParamUsed(layer, prm));
                                if (prmUsed) continue;

                                removeItems.Add(typeName + " Parameter: " + prm);
                                if (perform) {
                                    controller.RemoveParameter(i);
                                    i--;
                                }
                            }
                        }

                        if (perform) VRCFuryEditorUtils.MarkDirty(controller);
                    }
                }

                var syncParams = VRCAvatarUtils.GetAvatarParams(avatar);
                if (syncParams != null) {
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(syncParams)) {
                        removeItems.Add("All Synced Params");
                        if (perform) VRCAvatarUtils.SetAvatarParams(avatar, null);
                    } else {
                        var prms = new List<VRCExpressionParameters.Parameter>(syncParams.parameters);
                        for (var i = 0; i < prms.Count; i++) {
                            if (ShouldRemoveParam != null && ShouldRemoveParam(prms[i].name)) {
                                removeItems.Add("Synced Param: " + prms[i].name);
                                if (perform) {
                                    prms.RemoveAt(i);
                                    i--;
                                }
                            }
                        }

                        if (perform) {
                            syncParams.parameters = prms.ToArray();
                            VRCFuryEditorUtils.MarkDirty(syncParams);
                        }
                    }
                }

                var m = VRCAvatarUtils.GetAvatarMenu(avatar);
                if (m != null) {
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(m)) {
                        removeItems.Add("All Avatar Menus");
                        if (perform) VRCAvatarUtils.SetAvatarMenu(avatar, null);
                    } else {
                        // Note: This is laid out strangely to avoid issues with menus that have recursive loops

                        var removeControls = new HashSet<VRCExpressionsMenu.Control>();
                        bool ShouldRemoveMenuItem(VRCExpressionsMenu.Control item) {
                            var shouldRemove =
                                item.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                                && item.subMenu
                                && ShouldRemoveAsset != null
                                && ShouldRemoveAsset(item.subMenu);
                            shouldRemove |=
                                item.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                                && item.subMenu
                                && item.subMenu.controls.Count > 0
                                && item.subMenu.controls.All(control => removeControls.Contains(control));
                            shouldRemove |=
                                item.type == VRCExpressionsMenu.Control.ControlType.Toggle
                                && item.parameter != null
                                && ShouldRemoveParam != null
                                && ShouldRemoveParam(item.parameter.name);
                            return shouldRemove;
                        }
                        while (true) {
                            var startRemoveCount = removeControls.Count;
                            m.ForEachMenu(ForEachItem: (item, path) => {
                                if (removeControls.Contains(item)) {
                                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Skip;
                                }

                                if (ShouldRemoveMenuItem(item)) {
                                    removeControls.Add(item);
                                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Skip;
                                }

                                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                            });
                            var endRemoveCount = removeControls.Count;
                            if (startRemoveCount == endRemoveCount) break;
                        }

                        m.ForEachMenu(ForEachItem: (item, path) => {
                            if (removeControls.Contains(item)) {
                                removeItems.Add("Menu Item: " + string.Join("/", path));
                                return perform
                                    ? VRCExpressionsMenuExtensions.ForEachMenuItemResult.Delete
                                    : VRCExpressionsMenuExtensions.ForEachMenuItemResult.Skip;
                            }
                            return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                        });
                    }
                }
            }

            return removeItems;
        }
        
        public static void RemoveComponent(UnityEngine.Component c) {
            if (c.gameObject.GetComponents<UnityEngine.Component>().Length == 2 && c.gameObject.transform.childCount == 0)
                RemoveObject(c.gameObject);
            else
                Object.DestroyImmediate(c);
        }
        public static void RemoveObject(VFGameObject obj) {
            if (!PrefabUtility.IsPartOfPrefabInstance(obj) || PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) {
                obj.Destroy();
            } else {
                foreach (var component in obj.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                    if (!(component is Transform)) {
                        Object.DestroyImmediate(component);
                    }
                }
                obj.name = "_deleted_" + obj.name;
            }
        }

        private static bool IsParamUsed(AnimatorControllerLayer layer, string param) {
            var isUsed = false;
            isUsed |= new AnimatorIterator.Conditions().From(layer)
                .Any(c => c.parameter == param);
            isUsed |= new AnimatorIterator.States().From(layer).Any(state =>
                state.speedParameter == param ||
                state.cycleOffsetParameter == param ||
                state.mirrorParameter == param ||
                state.timeParameter == param
            );
            isUsed |= new AnimatorIterator.Trees().From(layer)
                .Any(tree => tree.blendParameter == param || tree.blendParameterY == param);
            return isUsed;
        }
    }
}
