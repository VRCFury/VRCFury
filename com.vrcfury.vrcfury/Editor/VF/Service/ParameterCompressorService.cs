using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Hooks;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using Random = System.Random;

namespace VF.Service {
    internal class ParameterCompressorService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;
        [VFAutowired] private readonly ExceptionService excService;
        [VFAutowired] private readonly MenuService menuService;
        private VRCExpressionsMenu menuReadOnly => menuService.GetReadOnlyMenu();

        public void Apply() {
            var paramz = paramsService.GetReadOnlyParams();
            if (paramz == null) paramz = VrcfObjectFactory.Create<VRCExpressionParameters>();
            var mutated = paramz.Clone();
            Apply(mutated);

            if (!paramz.IsSameAs(mutated)) {
                paramsService.GetParams().GetRaw().parameters = paramz.parameters;
            }
        }

        private void Apply(VRCExpressionParameters paramz) {
            paramz.RemoveDuplicates();

            OptimizationDecision decision;
            if (BuildTargetUtils.IsDesktop()) {
                decision = AlignForDesktop(paramz);
            } else {
                decision = AlignForMobile(paramz);
            }

            if (!decision.compress.Any()) {
                return;
            }

            var (numberBatches, boolBatches) = decision.GetBatches();

            VRCExpressionParameters.Parameter MakeParam(string name, VRCExpressionParameters.ValueType type, bool synced) {
                var param = new VRCExpressionParameters.Parameter {
                    name = controllers.MakeUniqueParamName(name),
                    valueType = type
                };
                param.SetNetworkSynced(synced);
                paramz.Add(param);
                return param;
            }

            var batchCount = Math.Max(numberBatches.Count, boolBatches.Count);
            var indexBitCount = decision.GetIndexBitCount();
            var syncIndex = Enumerable.Range(0, indexBitCount)
                .Select(i => fx.NewBool($"SyncIndex{i}", synced: true))
                .ToArray();
            var syncInts = Enumerable.Range(0, decision.numberSlots)
                .Select(i => MakeParam("SyncDataNum" + i, VRCExpressionParameters.ValueType.Int, true))
                .ToList();
            var syncBools = Enumerable.Range(0, decision.boolSlots)
                .Select(i => MakeParam("SyncDataBool" + i, VRCExpressionParameters.ValueType.Bool, true))
                .ToList();

            var layer = fx.NewLayer("Parameter Compressor");
            var entry = layer.NewState("Entry").Move(0, 2);
            entry.TransitionsFromAny().When(fx.IsAnimatorEnabled().IsFalse());
            var remoteLost = layer.NewState("Receive (Lost)").Move(entry, 0, 1);
            entry.TransitionsTo(remoteLost).When(fx.IsLocal().IsFalse());

            Action doAtEnd = () => { };
            Action<VFState,VFState,VFCondition> whenNextStateReady = null;
            VFState sendLatchState = null;
            VFState recvUnlatchState = null;
            var yOffset = 2;
            foreach (var batchNum in Enumerable.Range(0, batchCount)) {
                var syncId = ((batchNum - 1) % ((1 << indexBitCount) - 1)) + 1;
                if (batchNum == 0) syncId = 0;
                var syncIds = Enumerable.Range(0, indexBitCount).Select(i => (syncId & 1<<(indexBitCount-1-i)) > 0).ToArray();
                var numberBatch = batchNum < numberBatches.Count ? numberBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();
                var boolBatch = batchNum < boolBatches.Count ? boolBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();

                // What is this sync index called?
                var title = $"({syncIds.Select(b => b ? "1" : "0").Join("")}):\n"
                            + numberBatch.Concat(boolBatch).Select(p => p.name).Join("\n");

                // Create and wire up send and receive states
                var latchTitlePrefix = (batchNum == 0) ? "Latch & " : "";
                var sendState = layer.NewState($"{latchTitlePrefix}Send {title}").Move(entry, -1, yOffset);
                var receiveConditions = new List<VFCondition>();
                foreach (var i in Enumerable.Range(0, indexBitCount)) {
                    doAtEnd += () => sendState.Drives(syncIndex[i], syncIds[i]);
                    receiveConditions.Add(syncIndex[i].Is(syncIds[i]));
                }
                var receiveState = layer.NewState($"{latchTitlePrefix}Receive {title}").Move(entry, batchNum == 0 ? 2 : 1, batchNum == 0 ? 1 : yOffset);
                var receiveCondition = VFCondition.All(receiveConditions);
                VFState receiveState2 = null;

                void WithReceiveState(Action<VFState> with) {
                    with.Invoke(receiveState);
                    if (receiveState2 != null) with.Invoke(receiveState2);
                }

                if (batchNum == 0) {
                    entry.TransitionsTo(sendState).When(fx.IsLocal().IsTrue());
                    receiveState2 = layer.NewState($"Receive {title}").Move(entry, 1, yOffset);
                    remoteLost.TransitionsTo(receiveState2).When(receiveCondition);
                    sendLatchState = sendState;
                    recvUnlatchState = receiveState;
                    doAtEnd += () => {
                        whenNextStateReady?.Invoke(sendState, receiveState, receiveCondition);
                    };
                }
                whenNextStateReady?.Invoke(sendState, receiveState, receiveCondition);
                whenNextStateReady = (nextSend, nextRecv, nextRecvCond) => {
                    sendState.TransitionsTo(nextSend).WithTransitionExitTime(0.1f).When();
                    WithReceiveState(rcv => {
                        rcv.TransitionsTo(nextRecv).When(nextRecvCond);
                        rcv.TransitionsTo(remoteLost).When(receiveCondition.Not().And(nextRecvCond.Not()));
                        rcv.TransitionsTo(remoteLost).WithTransitionExitTime(0.15f).When();
                    });
                };
                
                yOffset += (int)Math.Ceiling((title.Split('\n').Length+1) / 5f);

                void SyncParam(VRCExpressionParameters.Parameter original, VRCExpressionParameters.Parameter slot) {
                    var latch = MakeParam(original.name, original.valueType, false);
                    VRCExpressionParameters.Parameter sendFrom;
                    if (batchNum == 0) {
                        // No need to latch things in the first batch when sending
                        sendFrom = original;
                    } else {
                        sendLatchState?.DrivesCopy(original.name, latch.name);
                        sendFrom = latch;
                    }
                    recvUnlatchState?.DrivesCopy(latch.name, original.name);
                    // We abuse doAtEnd here, since the latch/unlatch needs to happen before the data drivers
                    if (original.valueType == VRCExpressionParameters.ValueType.Float) {
                        doAtEnd += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name, -1, 1, 0, 254);
                            WithReceiveState(rcv => rcv.DrivesCopy(slot.name, latch.name, 0, 254, -1, 1));
                        };
                    } else if (original.valueType == VRCExpressionParameters.ValueType.Int || original.valueType == VRCExpressionParameters.ValueType.Bool) {
                        doAtEnd += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name);
                            WithReceiveState(rcv => rcv.DrivesCopy(slot.name, latch.name));
                        };
                    } else {
                        throw new Exception("Unknown type?");
                    }
                }
                foreach (var slotNum in Enumerable.Range(0, numberBatch.Count())) {
                    SyncParam(numberBatch[slotNum], syncInts[slotNum]);
                }
                foreach (var slotNum in Enumerable.Range(0, boolBatch.Count())) {
                    SyncParam(boolBatch[slotNum], syncBools[slotNum]);
                }
            }
            doAtEnd?.Invoke();

            var originalCost = paramz.CalcTotalCost();
            foreach (var param in decision.compress) {
                param.SetNetworkSynced(false);
            }
            var newCost = paramz.CalcTotalCost();

            var wdoff = fx.GetLayers().SelectMany(layer => layer.allStates).Any(state => !state.writeDefaultValues);
            if (wdoff) {
                foreach (var state in layer.allStates) {
                    state.writeDefaultValues = false;
                }
            }

            Debug.Log($"Parameter Compressor: Compressed {originalCost} bits into {newCost} bits.");
        }

        private OptimizationDecision GetParamsToOptimize(VRCExpressionParameters paramz) {
            var originalCost = paramz.CalcTotalCost();
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            if (originalCost <= maxCost) {
                return new OptimizationDecision();
            }

            var drivenParams = new HashSet<string>();
            var addDrivenParams = new HashSet<string>();

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
                () => new ParamSelectionOptions { includeToggles = true, includeRadials = true },
                () => new ParamSelectionOptions { includeToggles = true, includeRadials = true, includePuppets = true },
                () => new ParamSelectionOptions { includeToggles = true, includeRadials = true, includePuppets = true, includeButtons = true },
            };

            var bestCost = originalCost;
            var bestDecision = new OptimizationDecision();
            var bestWasSuccess = false;
            ParamSelectionOptions bestParameterOptions = null;
            foreach (var attemptOptionFunc in attemptOptions) {
                var options = attemptOptionFunc.Invoke();
                var decision = GetParamsToOptimize(paramz, options, addDrivenParams, originalCost);
                var cost = decision.GetFinalCost(originalCost);
                if (cost < bestCost) {
                    bestCost = cost;
                    bestDecision = decision;
                    bestParameterOptions = options;
                }
                if (cost <= maxCost) {
                    bestWasSuccess = true;
                    break;
                }
            }

            var setting = CompressorMenuItem.Get();
            if (bestWasSuccess) {
                if (setting == CompressorMenuItem.Value.Compress) return bestDecision;
                if (setting == CompressorMenuItem.Value.Ask) {
                    var msg = $"Your avatar is out of space for parameters! Your avatar uses {originalCost}/{maxCost} bits.";
                    msg += " VRCFury can compress your parameters to fit, at the expense of slightly slower toggle syncing in game. Is this okay?";
                    var ok = EditorUtility.DisplayDialog("Out of parameter space", msg, "Ok (Accept Compression)", "Fail the Build");
                    if (ok) return bestDecision;
                }
            }

            var nonMenuParams = new HashSet<string>(paramz.parameters
                .Where(p => p.IsNetworkSynced())
                .Select(p => p.name));
            nonMenuParams.ExceptWith(GetParamsUsedInMenu(null));
            nonMenuParams.ExceptWith(drivenParams);
            nonMenuParams.RemoveWhere(s => s.StartsWith("FT/"));

            var errorMessage = $"Your avatar is out of space for parameters! Your avatar uses {originalCost}/{maxCost} bits.";

            if (!bestWasSuccess && bestCost < originalCost && setting != CompressorMenuItem.Value.Fail) {
                errorMessage +=
                    " VRCFury attempted to compress your parameters to fit, but even with maximum compression," +
                    $" VRCFury could only get it down to {bestCost}/{maxCost} bits.";
            }

            errorMessage += " Ask your avatar creator, or the creator of the last prop you've added, " +
                            "if there are any parameters you can remove to make space.";

            if (nonMenuParams.Count > 0 && setting != CompressorMenuItem.Value.Fail) {
                errorMessage += "\n\n"
                                + "These parameters were not compressable because they are not used in your menu, and not driven. If these aren't related to OSC, you should probably delete them:\n"
                                + nonMenuParams.JoinWithMore(20);
            }

            excService.ThrowIfActuallyUploading(new SneakyException(errorMessage));
            return new OptimizationDecision();
        }

        public class ParamSelectionOptions {
            public bool includeToggles;
            public bool includeRadials;
            public bool includePuppets;
            public bool includeButtons;
        }

        private ISet<string> GetParamsUsedInMenu([CanBeNull] ParamSelectionOptions options) {
            var paramNames = new HashSet<string>();
            void AttemptToAdd([CanBeNull] VRCExpressionsMenu.Control.Parameter param) {
                if (param == null) return;
                if (string.IsNullOrEmpty(param.name)) return;
                paramNames.Add(param.name);
            }

            // Don't use MenuService to avoid making a clone if this isn't a vrcfury asset
            if (menuReadOnly != null) {
                menuReadOnly.ForEachMenu(ForEachItem: (control, list) => {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet && (options == null || options.includeRadials)) {
                        AttemptToAdd(control.GetSubParameter(0));
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Button && (options == null || options.includeButtons)) {
                        AttemptToAdd(control.parameter);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Toggle && (options == null || options.includeToggles)) {
                        AttemptToAdd(control.parameter);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet && (options == null || options.includePuppets)) {
                        AttemptToAdd(control.GetSubParameter(0));
                        AttemptToAdd(control.GetSubParameter(1));
                        AttemptToAdd(control.GetSubParameter(2));
                        AttemptToAdd(control.GetSubParameter(3));
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet && (options == null || options.includePuppets)) {
                        AttemptToAdd(control.GetSubParameter(0));
                        AttemptToAdd(control.GetSubParameter(1));
                    }

                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });
            }
            return paramNames;
        }

        private OptimizationDecision GetParamsToOptimize(VRCExpressionParameters paramz, ParamSelectionOptions options, ISet<string> addDriven, int originalCost) {
            var eligible = new List<VRCExpressionParameters.Parameter>();
            var usedInMenu = GetParamsUsedInMenu(options);

            foreach (var param in paramz.parameters) {
                if (!param.IsNetworkSynced()) continue;
                if (!usedInMenu.Contains(param.name)) continue;
                if (addDriven.Contains(param.name) && !options.includePuppets) continue;
                eligible.Add(param);
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

        private class OptimizationDecision {
            public int numberSlots = 0;
            public int boolSlots = 0;
            public IList<VRCExpressionParameters.Parameter> compress = new VRCExpressionParameters.Parameter[] { };

            public OptimizationDecision TempCopy(Action<OptimizationDecision> with) {
                var copy = new OptimizationDecision {
                    numberSlots = numberSlots,
                    boolSlots = boolSlots,
                    compress = compress.ToList()
                };
                with.Invoke(copy);
                return copy;
            }

            public int GetIndexBitCount() {
                var batches = GetBatches();
                var batchCount = Math.Max(batches.numberBatches.Count, batches.boolBatches.Count);
                if (batchCount <= 2) {
                    return 1;
                } else {
                    return 2;
                }
            }

            public int GetFinalCost(int originalCost) {
                return originalCost
                       + GetIndexBitCount()
                       + numberSlots * 8
                       + boolSlots
                       - compress.Sum(p => VRCExpressionParameters.TypeCost(p.valueType));
            }

            public (
                List<List<VRCExpressionParameters.Parameter>> numberBatches,
                List<List<VRCExpressionParameters.Parameter>> boolBatches
            ) GetBatches() {
                var numbersToOptimize =
                    compress.Where(i => i.valueType != VRCExpressionParameters.ValueType.Bool).ToList();
                var boolsToOptimize =
                    compress.Where(i => i.valueType == VRCExpressionParameters.ValueType.Bool).ToList();
                var numberBatches = numbersToOptimize
                    .Chunk(numberSlots)
                    .Select(chunk => chunk.ToList())
                    .ToList();
                var boolBatches = boolsToOptimize
                    .Chunk(boolSlots)
                    .Select(chunk => chunk.ToList())
                    .ToList();
                return (numberBatches, boolBatches);
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
                var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
                //maxCost = 50;
                while (true) {
                    if (numberSlots < numberCount
                        && TempCopy(o => o.numberSlots++).GetFinalCost(originalCost) <= maxCost
                        && (boolCount == 0 || (float)numberSlots / numberCount < (float)boolSlots / boolCount)
                    ) {
                        numberSlots++;
                    } else if (boolSlots < boolCount
                        && TempCopy(o => o.boolSlots++).GetFinalCost(originalCost) <= maxCost
                    ) {
                        boolSlots++;
                    } else {
                        break;
                    }
                }
            }
        }

        private OptimizationDecision AlignForMobile(VRCExpressionParameters paramz) {
            if (!AlignMobileParamsMenuItem.Get()) {
                return GetParamsToOptimize(paramz);
            }

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
                return GetParamsToOptimize(paramz);
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
            var mobileParams = paramz.Clone().parameters.ToArray();
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

        private OptimizationDecision AlignForDesktop(VRCExpressionParameters paramz) {
            var paramsToOptimize = GetParamsToOptimize(paramz);
            if (IsActuallyUploadingHook.Get()) {
                var paramList = paramz.parameters.Select(p => {
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
