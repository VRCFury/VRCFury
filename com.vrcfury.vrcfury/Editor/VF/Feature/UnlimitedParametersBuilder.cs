using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VF.Utils.VRCExpressionsMenuExtensions.ForEachMenuItemResult;

namespace VF.Feature {
    [FeatureTitle("Unlimited Params (BETA)")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class UnlimitedParametersBuilder : FeatureBuilder<UnlimitedParameters> {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        [VFAutowired] private readonly DbtLayerService dbtLayerService;

        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        [FeatureBuilderAction(FeatureOrder.UnlimitedParameters)]
        public void Apply() {
            if (networkSyncedField == null) {
                throw new Exception("Your VRCSDK is too old to support the Unlimited Parameters component.");
            }

            var paramsToOptimize = GetParamsToOptimize();
            var bits = paramsToOptimize.Sum(p => VRCExpressionParameters.TypeCost(p.type));
            if (bits <= 16) return; // don't optimize 16 bits or less

            foreach (var param in paramsToOptimize) {
                var vrcPrm = paramz.GetParam(param.name);
                networkSyncedField.SetValue(vrcPrm, false);
            }

            var syncPointer = fx.NewInt("SyncPointer", synced: true);
            var syncData = fx.NewInt("SyncData", synced: true);

            var layer = fx.NewLayer("Unlimited Parameters");
            var entry = layer.NewState("Entry").Move(-3, -1);
            var local = layer.NewState("Local").Move(0, 2);
            entry.TransitionsTo(local).When(fx.IsLocal().IsTrue());

            var math = dbtLayerService.GetMath(dbtLayerService.Create());

            Action addRoundRobins = () => { };
            Action addDefault = () => { };
            for (int i = 0; i < paramsToOptimize.Count; i++) {
                var syncIndex = i + 1;
                var src = paramsToOptimize[i];
                var lastValue = fx.NewFloat($"{src.name}/LastSynced", def: -100);

                var sendState = layer.NewState($"Send {src.name}");
                if (i == 0) sendState.Move(local, 1, 0);
                sendState
                    .DrivesCopy(src.name, lastValue)
                    .Drives(syncPointer, syncIndex)
                    .TransitionsTo(local)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                
                VFAFloat currentValue;
                if (src.type == VRCExpressionParameters.ValueType.Float) {
                    currentValue = fx.NewFloat(src.name, usePrefix: false);
                    sendState.DrivesCopy(src.name, syncData, -1, 1, 0, 254);
                } else if (src.type == VRCExpressionParameters.ValueType.Int) {
                    currentValue = fx.NewFloat($"{src.name}/Current");
                    local.DrivesCopy(src.name, currentValue);
                    sendState.DrivesCopy(src.name, syncData);
                } else {
                    throw new Exception("Unknown type?");
                }
                var diff = math.Subtract(currentValue, lastValue);

                local.TransitionsTo(sendState)
                    .When(diff.AsFloat().IsLessThan(0).Or(diff.AsFloat().IsGreaterThan(0)));
                if (i == 0) {
                    addDefault = () => {
                        local.TransitionsTo(sendState).When(fx.Always());
                    };
                } else {
                    var fromI = syncIndex - 1; // Needs to be set outside the lambda
                    addRoundRobins += () => {
                        local.TransitionsTo(sendState).When(syncPointer.IsEqualTo(fromI));
                    };
                }
            }
            addRoundRobins();
            addDefault();

            // Receive
            entry.TransitionsToExit().When(fx.Always());
            for (int i = 0; i < paramsToOptimize.Count; i++) {
                var syncIndex = i + 1;
                var dst = paramsToOptimize[i];
                var receiveState = layer.NewState($"Receive {dst.name}");
                if (i == 0) {
                    receiveState.Move(local, 3, 0);
                }
                receiveState
                    .TransitionsToExit()
                    .When(fx.Always());
                if (dst.type == VRCExpressionParameters.ValueType.Float) {
                    receiveState.DrivesCopy(syncData, dst.name, 0, 254, -1, 1);
                } else if (dst.type == VRCExpressionParameters.ValueType.Int) {
                    receiveState.DrivesCopy(syncData, dst.name);
                } else {
                    throw new Exception("Unknown type?");
                }
                receiveState.TransitionsFromEntry().When(syncPointer.IsEqualTo(syncIndex));
            }

            Debug.Log($"Radial Toggle Optimizer: Reduced {bits} bits into 16 bits.");
        }

        private IList<(string name,VRCExpressionParameters.ValueType type)> GetParamsToOptimize() {
            var paramsToOptimize = new HashSet<(string,VRCExpressionParameters.ValueType)>();
            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;
                
                var vrcParam = paramz.GetParam(paramName);
                if (vrcParam == null) return;
                var networkSynced = (bool)networkSyncedField.GetValue(vrcParam);
                if (!networkSynced) return;

                if (vrcParam.valueType != VRCExpressionParameters.ValueType.Int &&
                    vrcParam.valueType != VRCExpressionParameters.ValueType.Float) {
                    return;
                }

                paramsToOptimize.Add((paramName, vrcParam.valueType));
            }

            menu.GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                }
                if (control.type == VRCExpressionsMenu.Control.ControlType.Button
                    || control.type == VRCExpressionsMenu.Control.ControlType.Toggle) {
                    AttemptToAdd(control.parameter?.name);
                }

                return Continue;
            });

            return paramsToOptimize.Take(255).ToList();
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This component will optimize all synced float parameters used in radial menu toggles into 16 total bits"));
            content.Add(VRCFuryEditorUtils.Warn(
                "This feature is in BETA - Please report any issues on the VRCFury discord"));
            return content;
        }
    }
}
