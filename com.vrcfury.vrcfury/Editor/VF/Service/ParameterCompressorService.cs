using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using VF.Builder;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Core;
using VRC.SDK3.Avatars.ScriptableObjects;
using Random = System.Random;

namespace VF.Service {
    [VFService]
    internal class ParameterCompressorService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;

        [FeatureBuilderAction(FeatureOrder.ParameterCompressor)]
        public void Apply() {
            IList<(string name, VRCExpressionParameters.ValueType type)> paramsToOptimize;
            if (BuildTargetUtils.IsDesktop()) {
                paramsToOptimize = AlignForDesktop();
            } else {
                paramsToOptimize = AlignForMobile();
            }

            if (!paramsToOptimize.Any()) {
                return;
            }

            var boolsInParallel = 8;

            var numbersToOptimize =
                paramsToOptimize.Where(i => i.type != VRCExpressionParameters.ValueType.Bool).ToList();
            var boolsToOptimize =
                paramsToOptimize.Where(i => i.type == VRCExpressionParameters.ValueType.Bool).ToList();
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
                vrcPrm.SetNetworkSynced(false);
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
            
            var model = globals.allFeaturesInRun.OfType<UnlimitedParameters>().FirstOrDefault();
            if (model == null) return paramsToOptimize.ToList();

            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;
                
                var vrcParam = paramz.GetParam(paramName);
                if (vrcParam == null) return;
                var networkSynced = vrcParam.IsNetworkSynced();
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

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });

            return paramsToOptimize.Take(255).ToList();
        }

        [Serializable]
        public class SavedData {
            public List<SavedParam> syncedParams = new List<SavedParam>();
            public string unityVersion;
            public string vrcfuryVersion;
            public int saveVersion;
        }

        [Serializable]
        public struct SavedParam {
            public VRCExpressionParameters.ValueType type;
            public string objectPath;
            public string paramName;
            public int offset;
            public bool compressed;

            public ParameterSourceService.Source ToSource() {
                return new ParameterSourceService.Source() {
                    objectPath = objectPath,
                    originalParamName = paramName,
                    offset = offset
                };
            }
        }

        [CanBeNull]
        private static string GetSavePath([CanBeNull] string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return null;
            return Path.Combine(localAppData, "VRCFury", "DesktopSyncData", blueprintId + ".json");
        }

        private IList<(string, VRCExpressionParameters.ValueType)> AlignForMobile() {
            // Mobile
            var blueprintId = avatarObject.GetComponent<PipelineManager>().NullSafe()?.blueprintId;
            var savePath = GetSavePath(blueprintId);
            if (savePath == null || !File.Exists(savePath)) {
                EditorUtility.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: You have not uploaded the desktop version of this avatar yet." +
                    " If you want parameters to sync properly, please upload the desktop version first.",
                    "Ok"
                );
                return GetParamsToOptimize();
            }

            var desktopDataStr = File.ReadAllText(savePath);
            var desktopData = JsonUtility.FromJson<SavedData>(desktopDataStr);

            if (desktopData.saveVersion != 1) {
                EditorUtility.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: You have not uploaded the desktop version of this avatar yet." +
                    " If you want parameters to sync properly, please upload the desktop version first.",
                    "Ok"
                );
                return GetParamsToOptimize();
            }
            if (desktopData.vrcfuryVersion != VRCFPackageUtils.Version) {
                EditorUtility.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: The desktop version of this avatar was uploaded with a different version of VRCFury." +
                    " If you want parameters to sync properly, please ensure the VRCFury version matches, and upload the desktop version first.\n\n" +
                    $"Desktop VRCFury Version: {desktopData.vrcfuryVersion}\n" +
                    $"This project's VRCFury Version: {VRCFPackageUtils.Version}",
                    "Ok"
                );
            }
            
            // Align params with desktop copy
            var mobileParams = paramz.GetRaw().Clone().parameters.ToArray();
            var mobileParamsBySource = mobileParams.ToDictionary(
                p => parameterSourceService.GetSource(p.name),
                p => p
            );
            var paramsToOptimize = new List<(string, VRCExpressionParameters.ValueType)>();
            var reordered = new List<VRCExpressionParameters.Parameter>();
            var rand = new Random().Next(100_000_000, 900_000_000);
            foreach (var desktopParam in desktopData.syncedParams) {
                if (mobileParamsBySource.TryGetValue(desktopParam.ToSource(), out var mobileParam)) {
                    mobileParam.valueType = desktopParam.type;
                    mobileParam.SetNetworkSynced(true);
                    if (desktopParam.compressed) {
                        paramsToOptimize.Add((mobileParam.name, mobileParam.valueType));
                    }
                    reordered.Add(mobileParam);
                } else {
                    var fillerName = $"__missing_param_from_desktop_{rand}_{desktopParam.paramName}";
                    reordered.Add(new VRCExpressionParameters.Parameter() {
                        name = fillerName,
                        valueType = desktopParam.type,
                    });
                    if (desktopParam.compressed) {
                        paramsToOptimize.Add((fillerName, desktopParam.type));
                    }
                }
            }

            var mobileExtras = mobileParams.Where(p => !reordered.Contains(p)).ToArray();
            var warnAboutExtras = mobileExtras.Where(p => p.IsNetworkSynced()).Select(p => {
                var source = parameterSourceService.GetSource(p.name);
                return source.originalParamName + " from " + source.objectPath;
            }).ToArray();
            foreach (var p in mobileExtras) {
                p.SetNetworkSynced(false);
            }
            reordered.AddRange(mobileExtras);
            if (warnAboutExtras.Any()) {
                EditorUtility.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: This mobile avatar contains parameters which will NOT sync, because they are not present in the desktop version." +
                    " If this is unexpected, make sure you upload the desktop version FIRST, and ensure the missing prefabs are in the same location in the hierarchy.\n\n"
                    + warnAboutExtras.Join('\n'),
                    "Ok"
                );
            }

            paramsService.GetParams().GetRaw().parameters = reordered.ToArray();
            return paramsToOptimize;
        }

        public IList<(string, VRCExpressionParameters.ValueType)> AlignForDesktop() {
            var paramsToOptimize = GetParamsToOptimize();
            if (IsActuallyUploadingHook.Get()) {
                var saveList = paramz.GetRaw().Clone().parameters.Where(p => p.IsNetworkSynced()).Select(p => {
                    var source = parameterSourceService.GetSource(p.name);
                    return new SavedParam() {
                        compressed = paramsToOptimize.Any(o => o.name == p.name),
                        objectPath = source.objectPath,
                        offset = source.offset,
                        paramName = source.originalParamName,
                        type = p.valueType
                    };
                }).ToList();
                var saveData = new SavedData() {
                    syncedParams = saveList,
                    saveVersion = 1,
                    unityVersion = Application.unityVersion,
                    vrcfuryVersion = VRCFPackageUtils.Version
                };
                var saveText = JsonUtility.ToJson(saveData, true);
                var originalAvatar = originalAvatarService.GetOriginal();
                WhenBlueprintIdReadyHook.Add(() => {
                    var blueprintId = originalAvatar.NullSafe()?.GetComponent<PipelineManager>().NullSafe()?.blueprintId;
                    var savePath = GetSavePath(blueprintId);
                    if (savePath != null) {
                        var dir = Path.GetDirectoryName(savePath);
                        if (dir != null) Directory.CreateDirectory(dir);
                        File.WriteAllBytes(savePath, Encoding.UTF8.GetBytes(saveText));
                    }
                });
            }
            return paramsToOptimize;
        }
    }
}
