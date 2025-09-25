using System;
using System.Collections.Generic;
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
using VF.Model.Feature;
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
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;

        private int maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;

        [FeatureBuilderAction(FeatureOrder.ParameterCompressor)]
        public void Apply() {
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            IList<(string name, VRCExpressionParameters.ValueType type)> paramsToOptimize;
            if (BuildTargetUtils.IsDesktop()) {
                paramsToOptimize = AlignForDesktop();
            } else {
                paramsToOptimize = AlignForMobile();
            }

            if (!paramsToOptimize.Any()) {
                return;
            }

            var numbersToOptimize =
                paramsToOptimize.Where(i => i.type != VRCExpressionParameters.ValueType.Bool).Take(255).ToList(); // max 255 numbers
            var boolsToOptimize =
                paramsToOptimize.Where(i => i.type == VRCExpressionParameters.ValueType.Bool).ToList();
            
            // calculate remaing space after all optimizable floats and bools are unsynced, add 8 for index
            var boolsInParallel = maxBits - (paramz.GetRaw().CalcTotalCost() - numbersToOptimize.Count() * 8 - boolsToOptimize.Count() + 8);

            if (boolsInParallel <= 0) boolsInParallel = 1; // just in case, it will fail later
            boolsToOptimize = boolsToOptimize.Take(boolsInParallel * 255).ToList(); // max 255 batches

            if (boolsToOptimize.Count <= boolsInParallel) boolsToOptimize.Clear(); // can fit all remaining bools without compression
            var boolBatches = boolsToOptimize.Select(i => i.name)
                .Chunk(boolsInParallel)
                .Select(chunk => chunk.ToList())
                .ToList();

            paramsToOptimize = numbersToOptimize.Concat(boolsToOptimize).ToList();

            var bitsToAdd = 8 + (numbersToOptimize.Any() ? 8 : 0) + (boolsToOptimize.Any() ? boolsInParallel : 0);
            var bitsToRemove = paramsToOptimize
                .Sum(p => VRCExpressionParameters.TypeCost(p.type));
            if (bitsToAdd >= bitsToRemove) paramsToOptimize.Clear(); // Don't optimize if it won't save space

            // save configuration if on desktop
            if (BuildTargetUtils.IsDesktop()) {
                SaveDesktop(paramsToOptimize);
            }

            if (paramsToOptimize.Count() == 0) return; // we're done

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
                    var shortcutCondition = diff.IsLessThan(0);
                    shortcutCondition = shortcutCondition.Or(diff.IsGreaterThan(0));
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
            var eligible = new HashSet<(string,VRCExpressionParameters.ValueType)>();
            
            var model = globals.allFeaturesInRun.OfType<UnlimitedParameters>().FirstOrDefault();
            if (model == null) return eligible.ToList();

            var addDriven = new HashSet<string>(controllers.GetAllUsedControllers()
                .SelectMany(controller => controller.layers)
                .SelectMany(layer => layer.allBehaviours)
                .OfType<VRCAvatarParameterDriver>()
                .SelectMany(driver => driver.parameters)
                .Where(p => p.type == VRC_AvatarParameterDriver.ChangeType.Add)
                .Select(p => p.name));

            // Go/Float is driven by an add driver, but it's safe to compress. The driver is only used while you're
            // actively holding a button in the menu.
            addDriven.Remove("Go/Float");

            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;

                if (addDriven.Contains(paramName)) return;
                
                var vrcParam = paramz.GetParam(paramName);
                if (vrcParam == null) return;
                var networkSynced = vrcParam.IsNetworkSynced();
                if (!networkSynced) return;

                var shouldOptimize = vrcParam.valueType == VRCExpressionParameters.ValueType.Int ||
                                      vrcParam.valueType == VRCExpressionParameters.ValueType.Float;

                shouldOptimize |= vrcParam.valueType == VRCExpressionParameters.ValueType.Bool && model.includeBools;

                if (shouldOptimize) {
                    eligible.Add((paramName, vrcParam.valueType));
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

            return paramz.GetRaw().parameters
                .Select(p => (p.name, p.valueType))
                .Where(p => eligible.Contains(p))
                .ToList();
        }

        [Serializable]
        public class SavedData {
            public List<SavedParam> parameters = new List<SavedParam>();
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

        private IList<(string, VRCExpressionParameters.ValueType)> AlignForMobile() {
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

            if (desktopData.saveVersion != 2) {
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

            var paramsToOptimize = new List<(string, VRCExpressionParameters.ValueType)>();
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
                    paramsToOptimize.Add((newParam.name, newParam.valueType));
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
            return paramsToOptimize;
        }

        public IList<(string, VRCExpressionParameters.ValueType)> AlignForDesktop() {
            var paramsToOptimize = GetParamsToOptimize();
             if (paramz.GetRaw().CalcTotalCost() <= maxBits && BuildTargetUtils.IsDesktop()) {
                Debug.Log($"No Parameter Compressing Required");
                paramsToOptimize.Clear();
            }
            return paramsToOptimize;
        }

        private void SaveDesktop(IList<(string name, VRCExpressionParameters.ValueType type)> paramsToOptimize){
            if (IsActuallyUploadingHook.Get()) {
                var paramList = paramz.GetRaw().Clone().parameters.Select(p => {
                    var source = parameterSourceService.GetSource(p.name);
                    return new SavedParam() {
                        parameter = p.Clone(),
                        source = source,
                        compressed = paramsToOptimize.Any(o => o.name == p.name)
                    };
                }).ToList();
                var saveData = new SavedData() {
                    parameters = paramList,
                    saveVersion = 2,
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
        }
    }
}
