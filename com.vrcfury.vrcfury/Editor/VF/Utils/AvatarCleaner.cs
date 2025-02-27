using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class AvatarCleaner {
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

            if (ShouldRemoveAsset != null) {
                var animators = avatarObj.GetComponentsInSelfAndChildren<Animator>();
                foreach (var animator in animators) {
                    if (animator.runtimeAnimatorController != null &&
                        ShouldRemoveAsset(animator.runtimeAnimatorController)) {
                        removeItems.Add("Animator Controller at path " + animator.owner().GetPath(avatarObj));
                        if (perform) {
                            animator.runtimeAnimatorController = null;
                            VRCFuryEditorUtils.MarkDirty(animator);
                        }
                    }
                }
            }

            if (ShouldRemoveObj != null || ShouldRemoveComponent != null) {
                var checkStack = new Stack<VFGameObject>();
                checkStack.Push(avatarObj);
                while (checkStack.Count > 0) {
                    var obj = checkStack.Pop();

                    // TODO: ShouldRemoveObj should maybe verify if anything else in the avatar is using the object
                    // (constraints and things)
                    
                    var removeObject = ShouldRemoveObj != null && ShouldRemoveObj(obj);
                    if (ShouldRemoveComponent != null && obj.childCount == 0) {
                        var allComponents = obj.GetComponents<UnityEngine.Component>()
                            .Where(c => c != null && !(c is Transform))
                            .ToArray();
                        if (allComponents.Length > 0) {
                            var allComponentsAreRemoved = allComponents.All(ShouldRemoveComponent);
                            removeObject |= allComponentsAreRemoved;
                        }
                    }

                    if (removeObject) {
                        removeItems.Add("Object: " + obj.GetPath(avatarObj));
                        if (perform) RemoveObject(obj);
                    } else {
                        if (ShouldRemoveComponent != null) {
                            foreach (var component in obj.GetComponents<UnityEngine.Component>()) {
                                if (component != null && !(component is Transform) && ShouldRemoveComponent(component)) {
                                    removeItems.Add(component.GetType().Name + " on " + obj.GetPath(avatarObj));
                                    if (perform) RemoveComponent(component);
                                }
                            }
                        }

                        // Make sure RemoveComponent didn't remove this object!
                        if (obj != null) {
                            foreach (var t2 in obj.Children()) checkStack.Push(t2);
                        }
                    }
                }
            }

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                foreach (var c in VRCAvatarUtils.GetAllControllers(avatar)) {
                    var controller_ = c.controller as AnimatorController;
                    if (controller_ == null) continue;
                    var controller = new VFController(controller_);
                    var typeName = VRCFEnumUtils.GetName(c.type);
                    if (ShouldRemoveAsset != null && ShouldRemoveAsset(controller_)) {
                        removeItems.Add("Avatar Controller: " + typeName);
                        if (perform) c.setToDefault();
                    } else {
                        var removedLayers = new HashSet<VFLayer>();
                        if (ShouldRemoveLayer != null) {
                            foreach (var layer in controller.GetLayers()) {
                                if (!ShouldRemoveLayer(layer.name)) continue;
                                removeItems.Add(typeName + " Layer: " + layer.name);
                                removedLayers.Add(layer);
                                if (perform) {
                                    layer.Remove();
                                }
                            }
                        }

                        if (ShouldRemoveParam != null) {
                            for (var i = 0; i < controller.parameters.Length; i++) {
                                var prm = controller.parameters[i].name;
                                if (!ShouldRemoveParam(prm)) continue;

                                var prmUsed = controller.GetLayers()
                                    .Where(layer => !removedLayers.Contains(layer))
                                    .Any(layer => IsParamUsed(layer, prm));
                                if (prmUsed) continue;

                                removeItems.Add(typeName + " Parameter: " + prm);
                                if (perform) {
                                    controller.RemoveParameter(i);
                                    i--;
                                }
                            }
                        }

                        if (perform) VRCFuryEditorUtils.MarkDirty(controller_);
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
                                && item.subMenu != null
                                && ShouldRemoveAsset != null
                                && ShouldRemoveAsset(item.subMenu);
                            shouldRemove |=
                                item.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                                && item.subMenu != null
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
                                removeItems.Add("Menu Item: " + path.Join('/'));
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
            if (c.owner().GetComponents<UnityEngine.Component>().Length == 2 && c.owner().childCount == 0)
                RemoveObject(c.owner());
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

        private static bool IsParamUsed(VFLayer layer, string param) {
            var isUsed = false;
            isUsed |= layer.allTransitions
                .SelectMany(t => t.conditions)
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
