using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Injector;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    [VFService]
    internal class AvatarCleaner {
        [VFAutowired] private readonly VRCFuryInjector injector;

        public List<string> Cleanup(
            VFGameObject avatarObj,
            bool perform = false,
            Func<VFGameObject, bool> ShouldRemoveObj = null,
            Func<UnityEngine.Component, bool> ShouldRemoveComponent = null,
            Func<string, bool> ShouldRemoveLayer = null,
            Func<string, bool> ShouldRemoveParam = null
        ) {
            var removeItems = new List<string>();

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
                        if (perform) obj.Destroy();
                    } else {
                        if (ShouldRemoveComponent != null) {
                            foreach (var component in obj.GetComponents<UnityEngine.Component>()) {
                                if (component != null && !(component is Transform) && ShouldRemoveComponent(component)) {
                                    removeItems.Add(component.GetType().Name + " on " + obj.GetPath(avatarObj));
                                    if (perform) component.Destroy();
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

            // Scene-only cleanup must not load, clone, or dirty any assets.
            if (ShouldRemoveLayer == null && ShouldRemoveParam == null) return removeItems;

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                // Scene-only cleanup also supports roots without an avatar descriptor.
                // Resolve avatar services only when their assets are actually being inspected.
                var controllers = injector.GetService<ControllersService>();
                foreach (var controller in controllers.GetAllUsedControllers()) {
                    var typeName = VRCFEnumUtils.GetName(controller.vrcType);
                    var removedLayers = new HashSet<VFLayer>();
                    if (ShouldRemoveLayer != null) {
                        foreach (var layer in controller.GetLayers()) {
                            if (!ShouldRemoveLayer(layer.name)) continue;
                            removeItems.Add(typeName + " Layer: " + layer.name);
                            removedLayers.Add(layer);
                        }
                        if (perform) {
                            foreach (var layer in removedLayers) layer.Remove();
                        }
                    }

                    if (ShouldRemoveParam != null) {
                        var removedParams = new HashSet<string>();
                        foreach (var parameter in controller.parameters) {
                            var prm = parameter.name;
                            if (!ShouldRemoveParam(prm)) continue;

                            var prmUsed = controller.GetLayers()
                                .Where(layer => !removedLayers.Contains(layer))
                                .Any(layer => IsParamUsed(layer, prm));
                            if (prmUsed) continue;

                            removeItems.Add(typeName + " Parameter: " + prm);
                            removedParams.Add(prm);
                        }
                        if (perform) {
                            foreach (var parameter in removedParams) controller.RemoveParameter(parameter);
                        }
                    }
                }
                if (perform) controllers.CommitRemovals();

                if (ShouldRemoveParam != null) {
                    var expressionParams = injector.GetService<ExpressionParamsService>();
                    var prms = new List<VRCExpressionParameters.Parameter>(expressionParams.GetSnapshot());
                    var originalCount = prms.Count;
                    for (var i = 0; i < prms.Count; i++) {
                        if (ShouldRemoveParam(prms[i].name)) {
                            removeItems.Add("Synced Param: " + prms[i].name);
                            if (perform) {
                                prms.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    if (perform && prms.Count != originalCount) {
                        var retainedNames = new HashSet<string>(prms.Select(p => p.name));
                        expressionParams.SetParameters(expressionParams.Where(p => retainedNames.Contains(p.name)));
                    }
                }

                var menuService = ShouldRemoveParam != null ? injector.GetService<MenuService>() : null;
                var m = menuService?.GetReadOnlyMenu();
                if (m != null && perform) {
                    m = menuService.GetMenu().GetRaw();
                }
                if (m != null) {
                    var removeControls = new HashSet<VRCExpressionsMenu.Control>();
                    bool ShouldRemoveMenuItem(VRCExpressionsMenu.Control item) {
                        var shouldRemove =
                            item.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                            && item.subMenu != null
                            && item.subMenu.controls.Count > 0
                            && item.subMenu.controls.All(control => removeControls.Contains(control));
                        shouldRemove |=
                            item.type == VRCExpressionsMenu.Control.ControlType.Toggle
                            && item.parameter != null
                            && ShouldRemoveParam(item.parameter.name);
                        return shouldRemove;
                    }
                    while (true) {
                        var startRemoveCount = removeControls.Count;
                        m.ForEachMenu(dirtyAll: false, ForEachItem: (item, path) => {
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

                    m.ForEachMenu(dirtyAll: false, ForEachItem: (item, path) => {
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

            return removeItems;
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
                .Any(tree => tree.BlendParameter == param || tree.BlendParameterY == param);
            return isUsed;
        }
    }
}
