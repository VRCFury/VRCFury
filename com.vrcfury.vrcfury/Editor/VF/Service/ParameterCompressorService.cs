using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using VF.Model;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using Random = System.Random;

namespace VF.Service {
    [VFService]
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
        
        private const float BATCH_TIME = 0.1f;

        public void Apply() {
            OptimizationDecisionWithInfo decisionWithInfo;
            {
                // This is written weird to make sure we don't clone the params if we don't have to
                var readOnlyParams = paramsService.GetReadOnlyParams();
                var mutated = readOnlyParams.Clone();
                mutated.RemoveDuplicates();
                if (BuildTargetUtils.IsDesktop()) {
                    decisionWithInfo = AlignForDesktop(mutated);
                } else {
                    decisionWithInfo = AlignForMobile(mutated);
                }
                if (!readOnlyParams.IsSameAs(mutated)) {
                    paramsService.GetParams().GetRaw().parameters = mutated.parameters;
                }
            }

            var decision = decisionWithInfo.decision;
            if (decision == null || !decision.compress.Any()) {
                return;
            }

            var paramz = paramsService.GetParams().GetRaw();
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
            // var clip = VrcfObjectFactory.Create<AnimationClip>();
            // clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "material._Color.r", 1);
            // clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "material._Color.g", 0);
            // clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "material._Color.b", 0);
            // clip.SetCurve("Body", typeof(SkinnedMeshRenderer), "material._Color.a", 1);
            // remoteLost.WithAnimation(clip);

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
                // We can't just go to the next send after 0.1s, because of a weird unity animator quirk where it will exit
                // "early" if it thinks the exit time is closer to the current frame than the next frame, which would potentially
                // make it update faster than the sync rate and lose a packet. To fix this, we have to add exactly one extra frame by going through
                // an additional state between each send.
                var sendStateExtraFrame = layer.NewState("Extra Frame").Move(entry, -2, yOffset);
                var sendState = layer.NewState($"{latchTitlePrefix}Send {title}").Move(entry, -1, yOffset);
                sendStateExtraFrame.TransitionsTo(sendState).When(fx.Always());
                // if (batchNum == 0) {
                //     sendState.WithAnimation(clip);
                // }
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
                        whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                    };
                }
                whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                whenNextStateReady = (nextSend, nextRecv, nextRecvCond) => {
                    sendState.TransitionsTo(nextSend).WithTransitionExitTime(BATCH_TIME).When();
                    WithReceiveState(rcv => {
                        //rcv.TransitionsTo(remoteLost).WithTransitionExitTime(BATCH_TIMEOUT).When();
                        rcv.TransitionsTo(nextRecv).When(nextRecvCond);
                        rcv.TransitionsTo(remoteLost).When(receiveCondition.Not().And(nextRecvCond.Not()));
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
            var compressNames = decision.compress.Select(p => p.name).ToImmutableHashSet();
            foreach (var param in paramz.parameters.Where(p => compressNames.Contains(p.name))) {
                param.SetNetworkSynced(false);
            }
            var newCost = paramz.CalcTotalCost();

            var wdoff = fx.GetLayers().SelectMany(l => l.allStates).Any(state => !state.writeDefaultValues);
            if (wdoff) {
                foreach (var state in layer.allStates) {
                    state.writeDefaultValues = false;
                }
            }
            NoBadControllerParamsService.UpgradeWrongParamTypes(fx);

            // Debug info
            {
                var options = decisionWithInfo.options;
                var types = options?.FormatTypes();

                var paramWarnings = decisionWithInfo.FormatWarnings(100);

                var minSyncTime = batchCount * BATCH_TIME;
                // Assume we just missed to the batch, so it has to do 2 full loops AND account for the extra
                // frame hack needed above, which can add half a frame per batch. Assume 30fps.
                var maxSyncTime = batchCount * (BATCH_TIME + (1 / 30f) * 0.5f) * 2;
                
                var debug = avatarObject.AddComponent<VRCFuryDebugInfo>();
                debug.title = "Parameter Compressor";
                debug.debugInfo =
                    "VRCFury compressed the parameters on this avatar to make them fit VRC's limit."
                    + $"\n\nOld Total: {FormatBitsPlural(originalCost)}"
                    + $"\nNew Total: {FormatBitsPlural(newCost)}"
                    + (!string.IsNullOrEmpty(types) ? $"\nCompressed types: {types}" : "")
                    + $"\nSync delay: {minSyncTime.ToString("N1")} - {maxSyncTime.ToString("N1")} seconds"
                    + $"\nBools per batch: {decision.boolSlots}"
                    + $"\nNumbers per batch: {decision.numberSlots}"
                    + $"\nBatches per sync: {batchCount}"
                    + (string.IsNullOrEmpty(paramWarnings) ? "" : $"\n\n{paramWarnings}");
                debug.warn = true;

                // Patch av3emu to use 0.1 sync time instead of its default (0.2)
                EditorApplication.delayCall += () => {
                    EditorApplication.delayCall += () => {
                        if (avatarObject == null) return;
                        var type = ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime");
                        var field = type?.GetField("NonLocalSyncInterval");
                        if (type == null || field == null) return;
                        var runtime = avatarObject.GetComponent(type);
                        if (runtime == null) return;
                        field.SetValue(runtime, 0.1f);
                    };
                };
            }
        }

        private OptimizationDecisionWithInfo GetParamsToOptimize(VRCExpressionParameters paramz) {
            var originalCost = paramz.CalcTotalCost();
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            if (originalCost <= maxCost) {
                return new OptimizationDecisionWithInfo();
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
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.RadialPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.RadialPuppet, ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.RadialPuppet, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.Toggle, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { ControlType.RadialPuppet, ControlType.Toggle, ControlType.TwoAxisPuppet, ControlType.FourAxisPuppet } },
            };

            var bestCost = originalCost;
            var bestDecision = new OptimizationDecision();
            var bestWasSuccess = false;
            var bestTime = 0f;
            ParamSelectionOptions bestParameterOptions = null;
            foreach (var attemptOptionFunc in attemptOptions) {
                var options = attemptOptionFunc.Invoke();
                var decision = GetParamsToOptimize(paramz, options.allowedMenuTypes.ToImmutableHashSet(), addDrivenParams, originalCost);
                var cost = decision.GetFinalCost(originalCost);
                if (cost >= bestCost) continue;
                var syncTime = decision.GetBatchCount() * BATCH_TIME;
                // If we already have a working solution, only accept a more aggressive option if it cuts the sync time at least in half
                if (bestWasSuccess && syncTime > bestTime / 2) continue;
                bestCost = cost;
                bestDecision = decision;
                bestParameterOptions = options;
                if (cost <= maxCost) {
                    bestWasSuccess = true;
                    if (syncTime <= 1) break; // If sync time is less than 1s, don't need to try any more aggressive options
                }
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
            var buttonMenuParamsStr = GetParamsUsedInMenu(new [] {ControlType.Button,ControlType.SubMenu}.ToImmutableHashSet());

            var allSyncedParams = new HashSet<VRCExpressionParameters.Parameter>(paramz.parameters.Where(p => p.IsNetworkSynced()).ToArray());
            var warnUnusedParams = allSyncedParams.Where(p => !controllerUsedParams.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnUnusedParams);
            var warnContactParams = allSyncedParams.Where(p => contactParams.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnContactParams);
            var warnButtonParams = allSyncedParams.Where(p => buttonMenuParamsStr.Contains(p.name)).ToList();
            allSyncedParams.ExceptWith(warnButtonParams);
            var warnOscOnlyParams = allSyncedParams.Where(p => !allMenuParamsStr.Contains(p.name) && !drivenParams.Contains(p.name) && !p.name.StartsWith("FT/")).ToList();
            allSyncedParams.ExceptWith(warnOscOnlyParams);

            var decisionWithInfo = new OptimizationDecisionWithInfo {
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
            return new OptimizationDecisionWithInfo();
        }

        public class ParamSelectionOptions {
            public IList<ControlType> allowedMenuTypes;

            public string FormatTypes() {
                return MenuTypePriority.Where(t => allowedMenuTypes.Contains(t)).Select(t => t.ToString()).Join(", ");
            }
        }

        private static readonly IList<ControlType> MenuTypePriority = new[] {
            // Make sure these are in priority order, since it matters if a param is used by multiple menu item types
            ControlType.RadialPuppet,
            ControlType.Toggle,
            ControlType.TwoAxisPuppet,
            ControlType.FourAxisPuppet,
            ControlType.Button,
            ControlType.SubMenu,
        };

        private ISet<string> GetParamsUsedInMenu(ISet<ControlType> allowedMenuTypes) {
            var paramNameToMenuType = new Dictionary<string, ControlType>();
            void AttemptToAdd(Parameter param, ControlType menuType) {
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
                    if (control.type == ControlType.RadialPuppet) {
                        AttemptToAdd(control.parameter, ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                    } else if (control.type == ControlType.Button) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == ControlType.Toggle) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == ControlType.FourAxisPuppet) {
                        AttemptToAdd(control.parameter, ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                        AttemptToAdd(control.GetSubParameter(2), control.type);
                        AttemptToAdd(control.GetSubParameter(3), control.type);
                    } else if (control.type == ControlType.TwoAxisPuppet) {
                        AttemptToAdd(control.parameter, ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                    } else if (control.type == ControlType.SubMenu) {
                        AttemptToAdd(control.parameter, ControlType.SubMenu);
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
            ISet<ControlType> allowedMenuTypes,
            ISet<string> addDriven,
            int originalCost
        ) {
            var eligible = new List<VRCExpressionParameters.Parameter>();
            var usedInMenu = GetParamsUsedInMenu(allowedMenuTypes);

            foreach (var param in paramz.parameters) {
                if (!param.IsNetworkSynced()) continue;
                if (!usedInMenu.Contains(param.name)) continue;
                if (addDriven.Contains(param.name) && !allowedMenuTypes.Contains(ControlType.FourAxisPuppet)) continue;
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

        private class OptimizationDecisionWithInfo {
            public OptimizationDecision decision;
            public ParamSelectionOptions options;
            public IList<VRCExpressionParameters.Parameter> warnUnusedParams;
            public IList<VRCExpressionParameters.Parameter> warnContactParams;
            public IList<VRCExpressionParameters.Parameter> warnButtonParams;
            public IList<VRCExpressionParameters.Parameter> warnOscOnlyParams;

            public string FormatWarnings(int maxCount) {
                var lines = new [] {
                    FormatWarnings("These params are totally unused in all controllers:", warnUnusedParams, maxCount),
                    FormatWarnings("These params are generated from a contact or physbone, which usually should happen on each remote, NOT synced:", warnContactParams, maxCount),
                    FormatWarnings("These params are used by a momentary button in your menu, which are often used by presets and often shouldn't be synced:", warnButtonParams, maxCount),
                    FormatWarnings("These params can only change using OSC, they canot change if you are not running an OSC app:", warnOscOnlyParams, maxCount),
                }.NotNull().Join("\n\n");
                if (!string.IsNullOrEmpty(lines)) {
                    return "Want to improve performance and reduce sync? VRCFury has detected that these params should possibly be marked as not network synced in your Parameters file:\n\n" + lines;
                }
                return "";
            }
            [CanBeNull]
            private static string FormatWarnings(string title, IList<VRCExpressionParameters.Parameter> list, int maxCount) {
                if (list == null || list.Count == 0) return null;
                return title + "\n" + list.Select(p => $"{p.name} ({FormatBitsPlural(p.TypeCost())})").JoinWithMore(maxCount);
            }
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
                if (GetBatchCount() <= 2) {
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
                       - compress.Sum(p => p.TypeCost());
            }

            public int GetBatchCount() {
                var batches = GetBatches();
                return Math.Max(batches.numberBatches.Count, batches.boolBatches.Count);
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

        private OptimizationDecisionWithInfo AlignForMobile(VRCExpressionParameters paramz) {
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

            return new OptimizationDecisionWithInfo {
                decision = new OptimizationDecision() {
                    boolSlots = desktopData.boolSlots,
                    numberSlots = desktopData.numberSlots,
                    compress = paramsToOptimize
                },
            };
        }

        private OptimizationDecisionWithInfo AlignForDesktop(VRCExpressionParameters paramz) {
            var decisionWithInfo = GetParamsToOptimize(paramz);
            if (IsActuallyUploadingHook.Get()) {
                var paramList = paramz.parameters.Select(p => {
                    var source = parameterSourceService.GetSource(p.name);
                    return new SavedParam() {
                        parameter = p.Clone(),
                        source = source,
                        compressed = decisionWithInfo.decision?.compress.Contains(p) ?? false
                    };
                }).ToList();
                var saveData = new SavedData() {
                    parameters = paramList,
                    saveVersion = 3,
                    unityVersion = Application.unityVersion,
                    vrcfuryVersion = VRCFPackageUtils.Version,
                    boolSlots = decisionWithInfo.decision?.boolSlots ?? 0,
                    numberSlots = decisionWithInfo.decision?.numberSlots ?? 0
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
            return decisionWithInfo;
        }

        private static string FormatBitsPlural(int numBits) {
            return numBits + " bit" + (numBits != 1 ? "s" : "");
        }
    }
}
