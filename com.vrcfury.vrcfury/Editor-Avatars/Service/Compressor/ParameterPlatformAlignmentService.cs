using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Exceptions;
using VF.Feature;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;

namespace VF.Service.Compressor {
    [VFService]
    internal class ParameterPlatformAlignmentService {
        [VFAutowired] private readonly PipelineIdService pipelineId;
        [VFAutowired] private readonly GlobalsService globals;
        private Lazy<SavedData> desktopUpload;
        private FullControllerPathAlignment.Controller[] fullControllers = Array.Empty<FullControllerPathAlignment.Controller>();

#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
        private const int SaveVersion = 12;
#else
        private const int SaveVersion = 13;
#endif

        [Serializable]
        internal class SavedData {
            public FullControllerPathAlignment.Controller[] fullControllers;
            public Parameter[] parameters;
            public string[] compressed;
            public int numberSlots;
            public int boolSlots;
            public string unityVersion;
            public string vrcfuryVersion;
            public int saveVersion;
        }

        [CanBeNull]
        private static string GetSavePath([CanBeNull] string blueprintId) {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData)) return null;
            return Path.Combine(localAppData, "VRCFury", "DesktopSyncData", blueprintId + ".json");
        }

        private static void Warn(string message) => DialogUtils.DisplayDialog("VRCFury Mobile Sync", message, "Ok");

        private SavedData GetDesktopUpload() {
            desktopUpload ??= new Lazy<SavedData>(() => {
                var id = pipelineId.Get();
                var path = GetSavePath(id);
                if (path == null || !File.Exists(path)) {
                    Warn("You have not uploaded the desktop version of this avatar yet." +
                        " To align mobile parameters, upload the desktop version first.\n\n" + id);
                    return null;
                }
                var data = JsonUtility.FromJson<SavedData>(File.ReadAllText(path));
                if (data.saveVersion != SaveVersion) {
                    throw new SneakyException(
                        "The desktop upload uses an incompatible parameter sync format. " +
                        "Use matching VRChat SDK and VRCFury versions and upload desktop first.\n\n" +
                        $"Desktop VRCFury Version: {data.vrcfuryVersion}\n" +
                        $"This project's VRCFury Version: {VRCFPackageUtils.Version}"
                    );
                }
                return data;
            });
            return desktopUpload.Value;
        }

        [FeatureBuilderAction(FeatureOrder.AlignFullControllerPaths)]
        public void PrepareFullControllers() {
            var builders = globals.allBuildersInRun.OfType<FullControllerBuilder>().ToArray();
            if (builders.Length == 0) return;
            if (BuildTargetUtils.IsDesktop()) {
                if (IsActuallyUploadingHook.Get()) {
                    fullControllers = FullControllerPathAlignment.Capture(globals.avatarObject, builders);
                }
            } else if (AlignMobileParamsMenuItem.Get() && GetDesktopUpload() is SavedData desktop) {
                FullControllerPathAlignment.Align(globals.avatarObject, builders, desktop.fullControllers);
            }
        }

        public ParameterCompressorSolverOutput AlignForMobile(List<Parameter> parameters) {
            if (!AlignMobileParamsMenuItem.Get()) return null;
            var desktop = GetDesktopUpload();
            return desktop == null ? null : AlignForMobile(parameters, desktop, Warn);
        }

        internal static ParameterCompressorSolverOutput AlignForMobile(
            List<Parameter> mobile,
            SavedData desktop,
            Action<string> warn = null
        ) {
            var byName = mobile.ToDictionary(p => p.name);
            var slots = new Dictionary<string, Parameter>();
            var matched = new HashSet<string>();
            foreach (var p in desktop.parameters) {
                var slot = p.Clone();
                if (byName.TryGetValue(p.name, out var local) && (!p.IsNetworkSynced() || local.IsNetworkSynced())) {
                    matched.Add(p.name);
                } else {
                    slot.name = "__missing_param_from_desktop_" + Guid.NewGuid().ToString("N");
                }
                slots.Add(p.name, slot);
            }
            // Keep desktop types and order for the wire. The drivers convert to each local declaration.
            var compressed = desktop.compressed.Select(name => slots[name]).ToArray();
            var wireNames = compressed.Select(p => p.name).ToHashSet();
            var extras = mobile.Where(p => !matched.Contains(p.name)).ToArray();
#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
            bool overBudget = mobile.Where(p => p.IsNetworkSynced()).Sum(p => p.TypeCost()) > ExpressionParamsService.GetMaxCost();
            if (compressed.Length > 0 || overBudget) DisableExtraSync();
            foreach (var p in compressed.Where(p => !byName.ContainsKey(p.name))) mobile.Add(p.Clone().Unsynced());
#else
            // Older SDKs also sync ordinary expression parameters by bit position.
            DisableExtraSync();
            mobile.Clear();
            mobile.AddRange(desktop.parameters.Select(p => wireNames.Contains(p.name) && matched.Contains(p.name)
                ? byName[p.name] : slots[p.name]));
            mobile.AddRange(extras);
#endif
            void DisableExtraSync() {
                var unsynced = extras.Where(p => p.IsNetworkSynced()).ToArray();
                if (unsynced.Length == 0) return;
                foreach (var p in unsynced) p.Unsynced();
                warn?.Invoke("These mobile parameters will not sync because they are missing from the desktop upload.\n\n" +
                    string.Join("\n", unsynced.Select(p => p.name)));
            }

            var decision = new OptimizationDecision {
                boolSlots = desktop.boolSlots,
                numberSlots = desktop.numberSlots,
                compress = compressed
            };
            var cost = mobile.Where(p => p.IsNetworkSynced() && !wireNames.Contains(p.name)).Sum(p => p.TypeCost());
            if (compressed.Length > 0) cost += decision.GetIndexBitCount() + decision.numberSlots * 8 + decision.boolSlots;
            if (cost > ExpressionParamsService.GetMaxCost()) {
                throw new SneakyException($"The aligned mobile avatar needs {cost} synced bits, exceeding its {ExpressionParamsService.GetMaxCost()}-bit budget. " +
                    "Reduce the bit widths of shared mobile parameters or compress more parameters on desktop.");
            }
            return new ParameterCompressorSolverOutput {
                decision = decision
            };
        }

        public void SaveToDiskAfterBuild(OptimizationDecision decision, IEnumerable<Parameter> parameters) {
            if (!IsActuallyUploadingHook.Get()) return;
            var path = GetSavePath(pipelineId.Get());
            if (path == null) return;
            var data = CaptureDesktopData(decision, parameters);
            WhenBlueprintIdReadyHook.Add(() => {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            });
        }

        internal SavedData CaptureDesktopData(OptimizationDecision decision, IEnumerable<Parameter> parameters) {
            return new SavedData {
                saveVersion = SaveVersion,
                unityVersion = Application.unityVersion,
                vrcfuryVersion = VRCFPackageUtils.Version,
                fullControllers = fullControllers,
#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
                parameters = parameters.Where(p => p.IsNetworkSynced()).Select(p => p.Clone()).ToArray(),
#else
                parameters = parameters.Select(p => p.Clone()).ToArray(),
#endif
                compressed = decision.compress.Select(p => p.name).ToArray(),
                numberSlots = decision.numberSlots,
                boolSlots = decision.boolSlots
            };
        }
    }
}
