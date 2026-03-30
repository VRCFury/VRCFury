using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;

namespace VF.Service.Compressor {
    /**
     * Decides what parameters need to be compressed for the optimal performance
     */
    [VFService]
    internal class ParameterCompressorSolverService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ExceptionService excService;
        [VFAutowired] private readonly MenuService menuService;
        private VRCExpressionsMenu menuReadOnly => menuService.GetReadOnlyMenu();
        [VFAutowired] private readonly ParamsService paramsService;

        public ParameterCompressorSolverOutput GetParamsToOptimize() {
            var paramz = paramsService.GetReadOnlyParams().Clone();
            var originalCost = paramz.CalcTotalCost();
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            if (originalCost <= maxCost) {
                return new ParameterCompressorSolverOutput();
            }

            var drivenParams = new HashSet<string>();
            var addDrivenParams = new HashSet<string>();
            var useBadPriorityMethod = false;

            // Avoid making this clone controllers
            foreach (var drivenParam in controllers.GetAllReadOnlyControllers()
                         .SelectMany(controller => controller.layers)
                         .SelectMany(layer => layer.allBehaviours)
                         .OfType<VRCAvatarParameterDriver>()
                         .SelectMany(driver => driver.parameters)) {
                drivenParams.Add(drivenParam.name);
                if (drivenParam.type == VRC_AvatarParameterDriver.ChangeType.Add) addDrivenParams.Add(drivenParam.name);
            }

            var attemptOptions = new Func<ParamSelectionOptions>[] {
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.Toggle, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.Toggle, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
            };

            var bestCost = originalCost;
            var bestDecision = new OptimizationDecision();
            var bestParameterOptions = new ParamSelectionOptions();
            var bestWasSuccess = false;
            var bestTime = 0f;
            foreach (var attemptOptionFunc in attemptOptions) {
                var options = attemptOptionFunc.Invoke();
                var decision = GetParamsToOptimize(paramz, options.allowedMenuTypes.ToImmutableHashSet(), addDrivenParams, originalCost, useBadPriorityMethod);
                var cost = decision.GetFinalCost(originalCost);
                var batchCount = decision.GetBatchCount();
                if (useBadPriorityMethod && batchCount > 255) {
                    // Bad priority method only supports up to 255 batches
                    continue;
                }
                var syncTime = batchCount * ParameterCompressorService.BATCH_TIME;
                if (bestWasSuccess) {
                    // If we already have a working solution,
                    // Only accept a more aggressive option if it cuts the sync time at least in half
                    if (syncTime > bestTime / 2) continue;
                    // Don't switch from a working solution to a non-working one
                    if (cost > maxCost) continue;
                } else {
                    // If we don't have a working solution yet, just try to find the lowest bits possible
                    if (cost >= bestCost) continue;
                }
                bestCost = cost;
                bestDecision = decision;
                bestParameterOptions = options;
                bestWasSuccess = cost <= maxCost;
                bestTime = syncTime;

                // If sync time is less than 1s, don't need to try any more aggressive options
                if (bestWasSuccess && syncTime <= 1) break;
            }

            var controllerUsedParams = new HashSet<string>();
            foreach (var c in controllers.GetAllReadOnlyControllers()) {
                c.RewriteParameters(p => {
                    controllerUsedParams.Add(p);
                    return p;
                }, includeWrites: false);
            }

            var contactParams = new HashSet<string>();
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                contactParams.Add(c.parameter);
            }
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                contactParams.Add(c.parameter + "_IsGrabbed");
                contactParams.Add(c.parameter + "_Angle");
                contactParams.Add(c.parameter + "_Stretch");
                contactParams.Add(c.parameter + "_Squish");
                contactParams.Add(c.parameter + "_IsPosed");
            }

            var allMenuParamsStr = GetParamsUsedInMenu(null);
            var buttonMenuParamsStr = GetParamsUsedInMenu(new [] {VRCExpressionsMenu.Control.ControlType.Button,VRCExpressionsMenu.Control.ControlType.SubMenu}.ToImmutableHashSet());

            var allSyncedParams = new HashSet<VRCExpressionParameters.Parameter>(paramz.parameters.Where(p => p.IsNetworkSynced()).ToArray());
            var warnUnusedParams = allSyncedParams.Where(p => !controllerUsedParams.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnUnusedParams);
            var warnContactParams = allSyncedParams.Where(p => contactParams.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnContactParams);
            var warnButtonParams = allSyncedParams.Where(p => buttonMenuParamsStr.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnButtonParams);
            var warnOscOnlyParams = allSyncedParams.Where(p => !allMenuParamsStr.Contains(p.name) && !drivenParams.Contains(p.name) && !p.name.StartsWith("FT/")).ToList();
            allSyncedParams.ExceptWith(warnOscOnlyParams);

            var decisionWithInfo = new ParameterCompressorSolverOutput {
                decision = bestDecision,
                warnUnusedParams = warnUnusedParams,
                warnContactParams = warnContactParams,
                warnButtonParams = warnButtonParams,
                warnOscOnlyParams = warnOscOnlyParams,
                options = bestParameterOptions
            };

            var setting = CompressorMenuItem.Get();
            if (bestWasSuccess) {
                if (setting == CompressorMenuItem.Value.Compress) return decisionWithInfo;
                if (setting == CompressorMenuItem.Value.Ask) {
                    var msg = $"Your avatar is out of space for parameters! Your avatar uses {originalCost}/{maxCost} bits.";
                    msg += " VRCFury can compress your parameters to fit, at the expense of slightly slower toggle syncing in game. Is this okay?";
                    var ok = EditorUtility.DisplayDialog("Out of parameter space", msg, "Ok (Accept Compression)", "Fail the Build");
                    if (ok) return decisionWithInfo;
                }
            }

            var errorMessage = $"Your avatar is out of space for parameters! Your avatar uses {originalCost}/{maxCost} bits.";

            if (!bestWasSuccess && bestCost < originalCost && setting != CompressorMenuItem.Value.Fail) {
                errorMessage +=
                    " VRCFury attempted to compress your parameters to fit, but even with maximum compression," +
                    $" VRCFury could only get it down to {bestCost}/{maxCost} bits.";
            }

            errorMessage += " Ask your avatar creator, or the creator of the last prop you've added, " +
                            "if there are any parameters you can remove to make space.";

            if (setting != CompressorMenuItem.Value.Fail) {
                var paramWarnings = decisionWithInfo.FormatWarnings(20);
                if (!string.IsNullOrEmpty(paramWarnings)) {
                    errorMessage += $"\n\n{paramWarnings}";
                }
            }

            excService.ThrowIfActuallyUploading(new SneakyException(errorMessage));
            return new ParameterCompressorSolverOutput();
        }

        public class ParamSelectionOptions {
            public IList<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes = new List<VRCExpressionsMenu.Control.ControlType>();

            public string FormatTypes() {
                return MenuTypePriority.Where(t => allowedMenuTypes.Contains(t)).Select(t => t.ToString()).Join(", ");
            }
        }

        private static readonly IList<VRCExpressionsMenu.Control.ControlType> MenuTypePriority = new[] {
            // Make sure these are in priority order, since it matters if a param is used by multiple menu item types
            VRCExpressionsMenu.Control.ControlType.RadialPuppet,
            VRCExpressionsMenu.Control.ControlType.Toggle,
            VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
            VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
            VRCExpressionsMenu.Control.ControlType.Button,
            VRCExpressionsMenu.Control.ControlType.SubMenu,
        };

        private ISet<string> GetParamsUsedInMenu(ISet<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes) {
            var paramNameToMenuType = new Dictionary<string, VRCExpressionsMenu.Control.ControlType>();
            void AttemptToAdd(VRCExpressionsMenu.Control.Parameter param, VRCExpressionsMenu.Control.ControlType menuType) {
                if (param == null) return;
                if (string.IsNullOrEmpty(param.name)) return;
                var menuTypePriority = MenuTypePriority.IndexOf(menuType);
                if (menuTypePriority < 0) return;
                if (paramNameToMenuType.TryGetValue(param.name, out var oldMenuType)) {
                    var oldMenuTypePriority = MenuTypePriority.IndexOf(oldMenuType);
                    if (menuTypePriority < oldMenuTypePriority) return;
                }
                paramNameToMenuType[param.name] = menuType;
            }

            // Don't use MenuService to avoid making a clone if this isn't a vrcfury asset
            if (menuReadOnly != null) {
                menuReadOnly.ForEachMenu(ForEachItem: (control, list) => {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Button) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Toggle) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                        AttemptToAdd(control.GetSubParameter(2), control.type);
                        AttemptToAdd(control.GetSubParameter(3), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                    }

                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });
            }
            return paramNameToMenuType
                .Where(pair => allowedMenuTypes == null || allowedMenuTypes.Contains(pair.Value))
                .Select(pair => pair.Key)
                .ToImmutableHashSet();
        }

        private OptimizationDecision GetParamsToOptimize(
            VRCExpressionParameters paramz,
            ISet<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes,
            ISet<string> addDriven,
            int originalCost,
            bool useBadPriorityMethod
        ) {
            var eligible = new List<VRCExpressionParameters.Parameter>();
            var usedInMenu = GetParamsUsedInMenu(allowedMenuTypes);

            foreach (var param in paramz.parameters) {
                if (!param.IsNetworkSynced()) continue;
                if (!usedInMenu.Contains(param.name)) continue;
                if (addDriven.Contains(param.name) && !allowedMenuTypes.Contains(VRCExpressionsMenu.Control.ControlType.FourAxisPuppet)) continue;
                eligible.Add(param);
            }

            var decision = new OptimizationDecision {
                compress = eligible,
                useBadPriorityMethod = useBadPriorityMethod
            };
            decision.Optimize(originalCost);

            return decision;
        }
    }
}
