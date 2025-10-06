using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
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
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;
        [VFAutowired] private readonly ExceptionService excService;

        [FeatureBuilderAction(FeatureOrder.ParameterCompressor)]
        public void Apply() {
            RemoveDuplicates();
            
            OptimizationDecision decision;
            if (BuildTargetUtils.IsDesktop()) {
                decision = AlignForDesktop();
            } else {
                decision = AlignForMobile();
            }

            if (!decision.compress.Any()) {
                return;
            }

            var (numberBatches, boolBatches) = decision.GetBatches();

            var syncPointer = fx.NewInt("SyncPointer", synced: true);
            var syncInts = Enumerable.Range(0, decision.numberSlots)
                .Select(i => fx.NewInt("SyncDataNum" + i, synced: true))
                .ToList();
            var syncBools = Enumerable.Range(0, decision.boolSlots)
                .Select(i => fx.NewBool("SyncDataBool" + i, synced: true))
                .ToList();

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
            var nextStateSpacing = 1;
            for (int i = 0; i < numberBatches.Count || i < boolBatches.Count; i++) {
                var syncIndex = i + 1;
                var numberBatch = i < numberBatches.Count ? numberBatches[i] : new List<VRCExpressionParameters.Parameter>();
                var boolBatch = i < boolBatches.Count ? boolBatches[i] : new List<VRCExpressionParameters.Parameter>();

                // What is this sync index called?
                var title = $"#{syncIndex}:\n" + numberBatch.Concat(boolBatch).Select(p => p.name).Join("\n");

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
                    sendState.Move(lastSendState, 0, nextStateSpacing);
                    receiveState.Move(lastReceiveState, 0, nextStateSpacing);
                }
                lastSendState = sendState;
                lastReceiveState = receiveState;
                nextStateSpacing = (int)Math.Ceiling((title.Split('\n').Length+1) / 5f);

                for (var slotNum = 0; slotNum < numberBatch.Count(); slotNum++) {
                    var originalParam = numberBatch[slotNum].name;
                    var type = numberBatch[slotNum].valueType;
                    var lastSynced = fx.NewFloat($"{originalParam}/LastSynced", def: -100);
                    sendState.DrivesCopy(originalParam, lastSynced);

                    VFAFloat currentValue;
                    if (type == VRCExpressionParameters.ValueType.Float) {
                        currentValue = fx.NewFloat(originalParam, usePrefix: false);
                        sendState.DrivesCopy(originalParam, syncInts[slotNum], -1, 1, 0, 254);
                        receiveState.DrivesCopy(syncInts[slotNum], originalParam, 0, 254, -1, 1);
                    } else if (type == VRCExpressionParameters.ValueType.Int) {
                        currentValue = fx.NewFloat($"{originalParam}/Current");
                        local.DrivesCopy(originalParam, currentValue);
                        sendState.DrivesCopy(originalParam, syncInts[slotNum]);
                        receiveState.DrivesCopy(syncInts[slotNum], originalParam);
                    } else {
                        throw new Exception("Unknown type?");
                    }
                    var diff = math.Subtract(currentValue, lastSynced);
                    var shortcutCondition = diff.IsLessThan(0);
                    shortcutCondition = shortcutCondition.Or(diff.IsGreaterThan(0));
                    local.TransitionsTo(sendState).When(shortcutCondition);
                }
                for (var slotNum = 0; slotNum < boolBatch.Count(); slotNum++) {
                    var originalParam = boolBatch[slotNum].name;
                    var lastSynced = fx.NewInt($"{originalParam}/LastSynced", def: -100);
                    sendState.DrivesCopy(originalParam, lastSynced);
                    sendState.DrivesCopy(originalParam, syncBools[slotNum]);
                    receiveState.DrivesCopy(syncBools[slotNum], originalParam);
                    var shortcutCondition = new VFABool(originalParam, false).IsTrue().And(lastSynced.IsLessThan(1));
                    shortcutCondition = shortcutCondition.Or(new VFABool(originalParam, false).IsFalse().And(lastSynced.IsGreaterThan(0)));
                    local.TransitionsTo(sendState).When(shortcutCondition);
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

            var originalCost = paramz.GetRaw().CalcTotalCost();
            foreach (var param in decision.compress) {
                param.SetNetworkSynced(false);
            }
            var newCost = paramz.GetRaw().CalcTotalCost();

            Debug.Log($"Parameter Compressor: Compressed {originalCost} bits into {newCost} bits.");
        }

        private void RemoveDuplicates() {
            var seenParams = new HashSet<string>();
            paramz.GetRaw().parameters = paramz.GetRaw().parameters.Where(p => seenParams.Add(p.name)).ToArray();
        }

        private OptimizationDecision GetParamsToOptimize() {
            var originalCost = paramz.GetRaw().CalcTotalCost();
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            if (originalCost <= maxCost) {
                return new OptimizationDecision();
            }

            var drivenParams = new HashSet<string>();
            var addDrivenParams = new HashSet<string>();

            foreach (var drivenParam in controllers.GetAllUsedControllers()
                         .SelectMany(controller => controller.layers)
                         .SelectMany(layer => layer.allBehaviours)
                         .OfType<VRCAvatarParameterDriver>()
                         .SelectMany(driver => driver.parameters)) {
                drivenParams.Add(drivenParam.name);
                if (drivenParam.type == VRC_AvatarParameterDriver.ChangeType.Add) addDrivenParams.Add(drivenParam.name);
            }

            // Go/Float is driven by an add driver, but it's safe to compress. The driver is only used while you're
            // actively holding a button in the menu.
            addDrivenParams.Remove("Go/Float");
            
            var decision = GetParamsToOptimize(false, false, addDrivenParams, originalCost);
            if (originalCost + decision.CalcOffset() <= maxCost) {
                return decision;
            }
            
            decision = GetParamsToOptimize(false, true, addDrivenParams, originalCost);
            if (originalCost + decision.CalcOffset() <= maxCost) {
                return decision;
            }
            
            decision = GetParamsToOptimize(true, false, addDrivenParams, originalCost);
            if (originalCost + decision.CalcOffset() <= maxCost) {
                return decision;
            }

            decision = GetParamsToOptimize(true, true, addDrivenParams, originalCost);
            if (originalCost + decision.CalcOffset() <= maxCost) {
                return decision;
            }

            var nonMenuParams = new HashSet<string>(paramz.GetRaw().parameters.Select(p => p.name));
            nonMenuParams.ExceptWith(GetParamsUsedInMenu(true));
            nonMenuParams.ExceptWith(drivenParams);

            var errorMessage =
                "Your avatar is out of space for parameters! Your avatar uses "
                + originalCost + "/" + maxCost
                + " bits.";

            if (decision.CalcOffset() < 0) {
                errorMessage +=
                    " VRCFury attempted to compress your parameters to fit, but even with maximum compression," +
                    " VRCFury could only get it down to " + (originalCost + decision.CalcOffset()) + "/" +
                    maxCost + " bits.";
            }
            
            errorMessage += " Ask your avatar creator, or the creator of the last prop you've added, " +
                            "if there are any parameters you can remove to make space.";

            if (nonMenuParams.Count > 0) {
                errorMessage += "\n\n"
                    + "These parameters were not compressable because they are not used in your menu, and not driven. If these aren't related to OSC, you should probably delete them:\n"
                    + nonMenuParams.JoinWithMore(20);
            }
            
            excService.ThrowIfActuallyUploading(new SneakyException(errorMessage));
            return new OptimizationDecision();
        }

        private ISet<string> GetParamsUsedInMenu(bool includePuppets) {
            var paramNames = new HashSet<string>();
            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;
                paramNames.Add(paramName);
            }
            menu.GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.Button || control.type == VRCExpressionsMenu.Control.ControlType.Toggle) {
                    AttemptToAdd(control.parameter?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet && includePuppets) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                    AttemptToAdd(control.GetSubParameter(1)?.name);
                    AttemptToAdd(control.GetSubParameter(2)?.name);
                    AttemptToAdd(control.GetSubParameter(3)?.name);
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet && includePuppets) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                    AttemptToAdd(control.GetSubParameter(1)?.name);
                }

                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return paramNames;
        }

        private OptimizationDecision GetParamsToOptimize(bool includePuppets, bool includeBools, ISet<string> addDriven, int originalCost) {

            var eligible = new List<VRCExpressionParameters.Parameter>();
            var usedInMenu = GetParamsUsedInMenu(includePuppets);

            foreach (var param in paramz.GetRaw().parameters) {
                if (!usedInMenu.Contains(param.name)) continue;
                if (addDriven.Contains(param.name)) continue;

                var networkSynced = param.IsNetworkSynced();
                if (!networkSynced) continue;

                var shouldOptimize = param.valueType == VRCExpressionParameters.ValueType.Int ||
                                     param.valueType == VRCExpressionParameters.ValueType.Float;

                shouldOptimize |= param.valueType == VRCExpressionParameters.ValueType.Bool && includeBools;

                if (shouldOptimize) {
                    eligible.Add(param);
                }
            }
            
            var decision = new OptimizationDecision {
                compress = eligible
            };
            decision.Optimize(originalCost);

            return decision;
        }

        [Serializable]
        public class SavedData {
            public List<SavedParam> parameters = new List<SavedParam>();
            public int numberSlots;
            public int boolSlots;
            public string unityVersion;
            public string vrcfuryVersion;
            public int saveVersion;
        }

        [Serializable]
        public struct SavedParam {
            public ParameterSourceService.Source source;
            public VRCExpressionParameters.Parameter parameter;
            public bool compressed;
        }

        [CanBeNull]
        private static string GetSavePath([CanBeNull] string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return null;
            return Path.Combine(localAppData, "VRCFury", "DesktopSyncData", blueprintId + ".json");
        }

        public static bool IsMobileBuildWithSavedData(VFGameObject avatarObject) {
            if (BuildTargetUtils.IsDesktop()) return false;
            var blueprintId = avatarObject.GetComponent<PipelineManager>().NullSafe()?.blueprintId;
            var savePath = GetSavePath(blueprintId);
            if (savePath == null || !File.Exists(savePath)) {
                return false;
            }
            return true;
        }

        private class OptimizationDecision {
            public int numberSlots = 0;
            public int boolSlots = 0;
            public IList<VRCExpressionParameters.Parameter> compress = new VRCExpressionParameters.Parameter[] { };

            public int CalcOffset() {
                return 8 + numberSlots * 8 + boolSlots
                    - compress.Sum(p => VRCExpressionParameters.TypeCost(p.valueType));
            }

            public (
                List<List<VRCExpressionParameters.Parameter>> numberBatches,
                List<List<VRCExpressionParameters.Parameter>> boolBatches
            ) GetBatches(int offsetNumberSlots = 0) {
                var numbersToOptimize =
                    compress.Where(i => i.valueType != VRCExpressionParameters.ValueType.Bool).ToList();
                var boolsToOptimize =
                    compress.Where(i => i.valueType == VRCExpressionParameters.ValueType.Bool).ToList();
                var numberBatches = numbersToOptimize
                    .Chunk(numberSlots + offsetNumberSlots)
                    .Select(chunk => chunk.ToList())
                    .ToList();
                var boolBatches = boolsToOptimize
                    .Chunk(boolSlots)
                    .Select(chunk => chunk.ToList())
                    .ToList();
                return (numberBatches, boolBatches);
            }

            public int GetNumRounds(int offsetNumberSlots = 0) {
                var batches = GetBatches(offsetNumberSlots);
                return Math.Max(batches.numberBatches.Count, batches.boolBatches.Count);
            }

            /**
             * Attempts to expand the number of used number and bool slots up until the avatar's bits are full,
             * to increase parallelism and reduce the time needed for a full sync.
             * If both bools and numbers are compressed, it attempts to keep the batch count the same so it's not
             * wasting time syncing only bools or only numbers during some batches.
             */
            public void Optimize(int originalCost) {
                var boolCount = compress.Count(p => p.valueType == VRCExpressionParameters.ValueType.Bool);
                var numberCount = compress.Count(p => p.valueType != VRCExpressionParameters.ValueType.Bool);
                boolSlots = boolCount > 0 ? 1 : 0;
                numberSlots = numberCount > 0 ? 1 : 0;
                var currentCost = originalCost + CalcOffset();
                var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
                while (true) {
                    if (numberSlots < numberCount && currentCost <= maxCost - 8 && GetNumRounds(1) < GetNumRounds()) {
                        numberSlots++;
                        currentCost += 8;
                    } else if (boolSlots < boolCount && currentCost <= maxCost - 1) {
                        boolSlots++;
                        currentCost += 1;
                    } else {
                        break;
                    }
                }
            }
        }

        private OptimizationDecision AlignForMobile() {
            // Mobile
            var blueprintId = avatarObject.GetComponent<PipelineManager>().NullSafe()?.blueprintId;
            var savePath = GetSavePath(blueprintId);
            if (savePath == null || !File.Exists(savePath)) {
                DialogUtils.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: You have not uploaded the desktop version of this avatar yet." +
                    " If you want parameters to sync properly, please upload the desktop version first.\n\n"
                    + blueprintId,
                    "Ok"
                );
                return GetParamsToOptimize();
            }

            var desktopDataStr = File.ReadAllText(savePath);
            var desktopData = JsonUtility.FromJson<SavedData>(desktopDataStr);

            if (desktopData.saveVersion != 3) {
                throw new SneakyException(
                    "The desktop version of this avatar was uploaded with an incompatible version of VRCFury." + 
                    " Please ensure the VRCFury version matches, and upload the desktop version first.\n\n" +
                    $"Desktop VRCFury Version: {desktopData.vrcfuryVersion}\n" +
                    $"This project's VRCFury Version: {VRCFPackageUtils.Version}"
                );
            }
            if (desktopData.vrcfuryVersion != VRCFPackageUtils.Version) {
                DialogUtils.DisplayDialog(
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

            // Find object path aliases (when two objects are supposed to sync, but are not named exactly the same)
            var desktopToMobilePathAliases = new Dictionary<string, string>();
            {
                var mobileParamSourcesByPath = mobileParamsBySource
                    .Select(pair => pair.Key)
                    .GroupBy(source => source.objectPath)
                    .ToDictionary(group => group.Key, group => group.ToList());
                var desktopParamSourcesByPath = desktopData.parameters
                    .Select(p => p.source)
                    .GroupBy(source => source.objectPath)
                    .ToDictionary(group => group.Key, group => group.ToList());

                desktopToMobilePathAliases["__global"] = "__global";
                foreach (var mobilePair in mobileParamSourcesByPath) {
                    var mobilePath = mobilePair.Key;
                    if (desktopParamSourcesByPath.ContainsKey(mobilePath)) {
                        desktopToMobilePathAliases[mobilePath] = mobilePath;
                    }
                }
                foreach (var mobilePair in mobileParamSourcesByPath) {
                    var mobilePath = mobilePair.Key;
                    if (desktopToMobilePathAliases.ContainsValue(mobilePath)) continue;
                    var mobileParamSourcesAtPath = mobilePair.Value;
                    var matchingDesktopPaths = desktopParamSourcesByPath.Where(desktopPair => {
                        var desktopPath = desktopPair.Key;
                        if (desktopToMobilePathAliases.ContainsKey(desktopPath)) return false;
                        var desktopParamSourcesAtPath = desktopPair.Value;
                        var matchingParams = mobileParamSourcesAtPath.Where(m =>
                            desktopParamSourcesAtPath.Any(d =>
                                m.originalParamName == d.originalParamName && m.offset == d.offset)).ToList();
                        return matchingParams.Count == mobileParamSourcesAtPath.Count;
                    }).Select(desktopPair => desktopPair.Key).ToList();
                    if (matchingDesktopPaths.Count == 1) {
                        desktopToMobilePathAliases[matchingDesktopPaths.First()] = mobilePath;
                    }
                }
            }

            var paramsToOptimize = new List<VRCExpressionParameters.Parameter>();
            var reordered = new List<VRCExpressionParameters.Parameter>();
            var matchedMobileParams = new List<VRCExpressionParameters.Parameter>();
            var rand = new Random().Next(100_000_000, 900_000_000);
            var fillerI = 0;
            foreach (var desktopEntry in desktopData.parameters) {
                var desktopSource = desktopEntry.source;
                var desktopParam = desktopEntry.parameter;
                if (desktopToMobilePathAliases.TryGetValue(desktopSource.objectPath, out var mobilePathAlias)) {
                    desktopSource.objectPath = mobilePathAlias;
                }

                var newParam = desktopParam.Clone();
                if (mobileParamsBySource.TryGetValue(desktopSource, out var matchingMobileParam)) {
                    newParam.name = matchingMobileParam.name;
                    matchedMobileParams.Add(matchingMobileParam);
                } else {
                    newParam.name = $"__missing_param_from_desktop_{rand}_{fillerI++}_{desktopParam.name}";
                }
                if (desktopEntry.compressed) {
                    paramsToOptimize.Add(newParam);
                }
                reordered.Add(newParam);
            }

            var mobileExtras = mobileParams.Where(p => !matchedMobileParams.Contains(p)).ToArray();
            var warnAboutExtras = mobileExtras.Where(p => p.IsNetworkSynced()).Select(p => {
                var source = parameterSourceService.GetSource(p.name);
                return source.originalParamName + " from " + source.objectPath;
            }).ToArray();
            foreach (var p in mobileExtras) {
                p.SetNetworkSynced(false);
            }
            reordered.AddRange(mobileExtras);
            if (warnAboutExtras.Any()) {
                DialogUtils.DisplayDialog(
                    "VRCFury Mobile Sync",
                    "Warning: This mobile avatar contains parameters which will NOT sync, because they are not present in the desktop version." +
                    " If this is unexpected, make sure you upload the desktop version FIRST, and ensure the missing prefabs are in the same location in the hierarchy.\n\n"
                    + warnAboutExtras.Join('\n'),
                    "Ok"
                );
            }

            paramsService.GetParams().GetRaw().parameters = reordered.ToArray();

            return new OptimizationDecision() {
                boolSlots = desktopData.boolSlots,
                numberSlots = desktopData.numberSlots,
                compress = paramsToOptimize
            };
        }

        private OptimizationDecision AlignForDesktop() {
            var paramsToOptimize = GetParamsToOptimize();
            if (IsActuallyUploadingHook.Get()) {
                var paramList = paramz.GetRaw().parameters.Select(p => {
                    var source = parameterSourceService.GetSource(p.name);
                    return new SavedParam() {
                        parameter = p.Clone(),
                        source = source,
                        compressed = paramsToOptimize.compress.Contains(p)
                    };
                }).ToList();
                var saveData = new SavedData() {
                    parameters = paramList,
                    saveVersion = 3,
                    unityVersion = Application.unityVersion,
                    vrcfuryVersion = VRCFPackageUtils.Version,
                    boolSlots = paramsToOptimize.boolSlots,
                    numberSlots = paramsToOptimize.numberSlots
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
