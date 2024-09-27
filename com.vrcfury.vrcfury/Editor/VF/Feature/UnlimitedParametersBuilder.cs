﻿using System;
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
    [FeatureAlias("Unlimited Parameters")]
    [FeatureTitle("Parameter Compressor")]
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
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }

            if (paramz.GetRaw().CalcTotalCost() <= maxBits) {
                Debug.Log($"No Parameter Compressing Required");
                return;
            }

            if (networkSyncedField == null) {
                throw new Exception("Your VRCSDK is too old to support VRCFury Parameter Compressor.");
            }

            var paramsToOptimize = GetParamsToOptimize();

            var numbersToOptimize =
                paramsToOptimize.Where(i => i.type != VRCExpressionParameters.ValueType.Bool).ToList();
            var boolsToOptimize =
                paramsToOptimize.Where(i => i.type == VRCExpressionParameters.ValueType.Bool).ToList();
            

            var boolsInParallel = maxBits - (paramz.GetRaw().CalcTotalCost() - numbersToOptimize.Count() * 8 - boolsToOptimize.Count() + 16);

            if (boolsInParallel <= 0) boolsInParallel = 1;

            if (boolsToOptimize.Count <= boolsInParallel) boolsToOptimize.Clear();
            var boolBatches = boolsToOptimize.Select(i => i.name)
                .Chunk(boolsInParallel)
                .Select(chunk => chunk.ToList())
                .ToList();

            paramsToOptimize = numbersToOptimize.Concat(boolsToOptimize).ToList();

            var bitsToAdd = 8 + (numbersToOptimize.Any() ? 8 : 0) + (boolsToOptimize.Any() ? boolsInParallel : 0);
            var bitsToRemove = paramsToOptimize
                .Sum(p => VRCExpressionParameters.TypeCost(p.type));
            if (bitsToAdd >= bitsToRemove) return; // Don't optimize if it won't save space

            foreach (var param in paramsToOptimize) {
                var vrcPrm = paramz.GetParam(param.name);
                networkSyncedField.SetValue(vrcPrm, false);
            }

            var syncPointer = fx.NewInt("SyncPointer", synced: true);
            VFAInteger syncData = null;
            if (numbersToOptimize.Any()) {
                syncData = fx.NewInt("SyncDataNum", synced: true);
            }
            var syncBools = new List<VFABool>();
            if (boolBatches.Any()) {
                syncBools.AddRange(Enumerable.Range(0, boolsInParallel)
                    .Select(i => fx.NewBool("SyncDataBool", synced: true)));
            }

            var layer = fx.NewLayer("Parameter Compressor");
            var entry = layer.NewState("Entry").Move(-3, -1);
            var local = layer.NewState("Local").Move(0, 2);
            entry.TransitionsTo(local).When(fx.IsLocal().IsTrue());
            entry.TransitionsToExit().When(fx.Always());

            var math = dbtLayerService.GetMath(dbtLayerService.Create());

            Action addRoundRobins = () => { };
            Action addDefault = () => { };
            VFState lastSendState = null;
            VFState lastReceiveState = null;
            for (int i = 0; i < numbersToOptimize.Count || i < boolBatches.Count; i++) {
                var syncIndex = i + 1;

                // What is this sync index called?
                var title = $"#{syncIndex}:";
                if (i < numbersToOptimize.Count) {
                    title += " " + paramsToOptimize[i].name;
                }
                if (i < boolBatches.Count) {
                    if (i < numbersToOptimize.Count) title += " +";
                    title += " Bool Batch " + syncIndex;
                }

                // Create and wire up send and receive states
                var sendState = layer.NewState($"Send {title}");
                var receiveState = layer.NewState($"Receive {title}");
                sendState
                    .Drives(syncPointer, syncIndex)
                    .TransitionsTo(local)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                receiveState.TransitionsFromEntry().When(syncPointer.IsEqualTo(syncIndex));
                receiveState.TransitionsToExit().When(fx.Always());
                if (i == 0) {
                    sendState.Move(local, 1, 0);
                    receiveState.Move(local, 3, 0);
                } else {
                    sendState.Move(lastSendState, 0, 1);
                    receiveState.Move(lastReceiveState, 0, 1);
                }
                lastSendState = sendState;
                lastReceiveState = receiveState;

                if (i < numbersToOptimize.Count) {
                    var (originalParam,type) = paramsToOptimize[i];
                    var lastSynced = fx.NewFloat($"{originalParam}/LastSynced", def: -100);
                    sendState.DrivesCopy(originalParam, lastSynced);

                    VFAFloat currentValue;
                    if (type == VRCExpressionParameters.ValueType.Float) {
                        currentValue = fx.NewFloat(originalParam, usePrefix: false);
                        sendState.DrivesCopy(originalParam, syncData, -1, 1, 0, 254);
                        receiveState.DrivesCopy(syncData, originalParam, 0, 254, -1, 1);
                    } else if (type == VRCExpressionParameters.ValueType.Int) {
                        currentValue = fx.NewFloat($"{originalParam}/Current");
                        local.DrivesCopy(originalParam, currentValue);
                        sendState.DrivesCopy(originalParam, syncData);
                        receiveState.DrivesCopy(syncData, originalParam);
                    } else {
                        throw new Exception("Unknown type?");
                    }
                    var diff = math.Subtract(currentValue, lastSynced);
                    var shortcutCondition = diff.AsFloat().IsLessThan(0);
                    shortcutCondition = shortcutCondition.Or(diff.AsFloat().IsGreaterThan(0));
                    local.TransitionsTo(sendState).When(shortcutCondition);
                }

                if (i < boolBatches.Count) {
                    var batch = boolBatches[i].ToArray();
                    for (var numWithinBatch = 0; numWithinBatch < batch.Count(); numWithinBatch++) {
                        var originalParam = batch[numWithinBatch];
                        var lastSynced = fx.NewInt($"{originalParam}/LastSynced", def: -100);
                        sendState.DrivesCopy(originalParam, lastSynced);
                        sendState.DrivesCopy(originalParam, syncBools[numWithinBatch]);
                        receiveState.DrivesCopy(syncBools[numWithinBatch], originalParam);
                        var shortcutCondition = new VFABool(originalParam, false).IsTrue().And(lastSynced.IsLessThan(1));
                        shortcutCondition = shortcutCondition.Or(new VFABool(originalParam, false).IsFalse().And(lastSynced.IsGreaterThan(0)));
                        local.TransitionsTo(sendState).When(shortcutCondition);
                    }
                }

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

            Debug.Log($"Radial Toggle Optimizer: Reduced {bitsToRemove} bits into {bitsToAdd} bits.");
        }

        private IList<(string name,VRCExpressionParameters.ValueType type)> GetParamsToOptimize() {
            var paramsToOptimize = new HashSet<(string,VRCExpressionParameters.ValueType)>();
            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;
                
                var vrcParam = paramz.GetParam(paramName);
                if (vrcParam == null) return;
                var networkSynced = (bool)networkSyncedField.GetValue(vrcParam);
                if (!networkSynced) return;

                var shouldOptimize = vrcParam.valueType == VRCExpressionParameters.ValueType.Int ||
                                      vrcParam.valueType == VRCExpressionParameters.ValueType.Float;

                shouldOptimize |= vrcParam.valueType == VRCExpressionParameters.ValueType.Bool && model.includeBools;

                if (shouldOptimize) {
                    paramsToOptimize.Add((paramName, vrcParam.valueType));
                }
            }

            menu.GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.Button || control.type == VRCExpressionsMenu.Control.ControlType.Toggle) {
                    AttemptToAdd(control.parameter?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet && model.includePuppets) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                    AttemptToAdd(control.GetSubParameter(1)?.name);
                    AttemptToAdd(control.GetSubParameter(2)?.name);
                    AttemptToAdd(control.GetSubParameter(3)?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet && model.includePuppets) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                    AttemptToAdd(control.GetSubParameter(1)?.name);
                }

                return Continue;
            });

            return paramsToOptimize.Take(255).ToList();
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This component will optimize all synced float parameters used in radial menu toggles into 16 total bits"));
            content.Add(VRCFuryEditorUtils.Warn(
                "This feature is in BETA - Please report any issues on the VRCFury discord"));

            var includeBoolsProp = prop.FindPropertyRelative("includeBools");
            content.Add(VRCFuryEditorUtils.Prop(includeBoolsProp, "Optimize Bools"));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (includeBoolsProp.boolValue) {
                    return VRCFuryEditorUtils.Warn(
                        "Warning: Compressing bools often doesn't save much space, and can, in some rare cases, cause unusual sync issues with complex avatar systems.");
                }
                return new VisualElement();
            }, includeBoolsProp));

            var includePuppetsProp = prop.FindPropertyRelative("includePuppets");
            content.Add(VRCFuryEditorUtils.Prop(includePuppetsProp, "Optimize Puppets"));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (includePuppetsProp.boolValue) {
                    return VRCFuryEditorUtils.Warn(
                        "Warning: Compressing puppets may cause them to not move as smoothly for remote clients as you control them.");
                }
                return new VisualElement();
            }, includePuppetsProp));

            return content;
        }
    }
}