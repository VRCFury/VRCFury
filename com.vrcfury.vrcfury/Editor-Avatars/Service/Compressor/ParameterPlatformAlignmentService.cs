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
using VF.Hooks;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using VRC.Core;
using VRC.SDK3.Avatars.ScriptableObjects;
using Random = System.Random;

namespace VF.Service.Compressor {
    [VFService]
    internal class ParameterPlatformAlignmentService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;
        [VFAutowired] private readonly OriginalAvatarService originalAvatarService;

        [Serializable]
        public class SavedData {
            public List<SavedParam> parameters = new List<SavedParam>();
            public int numberSlots;
            public int boolSlots;
            public bool useBadPriorityMethod;
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

        [CanBeNull]
        public ParameterCompressorSolverOutput AlignForMobile(VRCExpressionParameters paramz) {
            if (!AlignMobileParamsMenuItem.Get()) {
                return null;
            }

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
                return null;
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

            paramz.parameters = reordered.ToArray();

            return new ParameterCompressorSolverOutput {
                decision = new OptimizationDecision {
                    boolSlots = desktopData.boolSlots,
                    numberSlots = desktopData.numberSlots,
                    compress = paramsToOptimize,
                    useBadPriorityMethod = desktopData.useBadPriorityMethod
                },
            };
        }

        public void SaveToDiskAfterBuild(OptimizationDecision decision, VRCExpressionParameters paramz) {
            if (!IsActuallyUploadingHook.Get()) return;

            var compressNames = decision.compress.Select(p => p.name).ToImmutableHashSet();
            var paramList = paramz.parameters.Select(p => {
                var source = parameterSourceService.GetSource(p.name);
                return new SavedParam() {
                    parameter = p.Clone(),
                    source = source,
                    compressed = compressNames.Contains(p.name)
                };
            }).ToList();
            var saveData = new SavedData() {
                parameters = paramList,
                saveVersion = 3,
                unityVersion = Application.unityVersion,
                vrcfuryVersion = VRCFPackageUtils.Version,
                boolSlots = decision.boolSlots,
                numberSlots = decision.numberSlots,
                useBadPriorityMethod = decision.useBadPriorityMethod
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