using System.Collections.Immutable;
using System.Linq;
using VF.Exceptions;
using VF.Injector;
using VF.Model;
using VF.Utils;

namespace VF.Service.Compressor {
    /**
     * Main entrypoint for compressing parameters to get them under the VRC limit
     */
    [VFService]
    internal class ParameterCompressorService {
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController actionController => controllers.GetAction();
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterCompressorSolverService solverService;
        [VFAutowired] private readonly ParameterCompressorLayerService newLayerService;
        [VFAutowired] private readonly ParameterCompressorLegacyLayerService legacyLayerService;
        [VFAutowired] private readonly ParameterPlatformAlignmentService platformAlignmentService;
        [VFAutowired] private readonly SaveAssetsService saveAssetsService;

        public const float BATCH_TIME = 0.1f;

        public void Apply() {
            ParameterCompressorSolverOutput decisionWithInfo;
            {
                var mutated = expressionParams.GetSnapshot()
                    .GroupBy(p => p.name).Select(group => group.First()).ToList();
                if (BuildTargetUtils.IsDesktop()) {
                    decisionWithInfo = solverService.GetParamsToOptimize();
                    platformAlignmentService.SaveToDiskAfterBuild(decisionWithInfo.decision, mutated);
                } else {
                    decisionWithInfo = platformAlignmentService.AlignForMobile(mutated);
                    if (decisionWithInfo == null) {
                        decisionWithInfo = solverService.GetParamsToOptimize();
                    }
                }
                if (!expressionParams.IsSameAs(mutated)) {
                    expressionParams.SetParameters(mutated);
                    expressionParams.WorkLog(
                        $"Updated {mutated.Count} expression parameters while preparing parameter compression"
                    );
                }
            }

            var decision = decisionWithInfo.decision;
            var unsupported = decision.compress.FirstOrDefault(p => !p.HasStandardNetworkEncoding());
            if (unsupported != null) {
                throw new SneakyException(
                    $"Parameter compression cannot preserve the network bit width or signedness of {unsupported.name}."
                    + " If this came from desktop sync data, rebuild and upload the desktop avatar first.");
            }
            if (!decision.compress.Any()) {
                saveAssetsService.Run(Enumerable.Empty<VFAvatarController>());
                return;
            }

            var originalCost = expressionParams.CalcTotalCost();

            if (decision.useBadPriorityMethod) {
                legacyLayerService.BuildLayer(decision, actionController);
            } else {
                newLayerService.BuildLayer(decision, actionController);
            }
            actionController.WorkLog(
                $"Added parameter compression logic to Action controller for {decision.compress.Count} parameters"
            );
            
            var compressNames = decision.compress.Select(p => p.name).ToImmutableHashSet();
            foreach (var param in expressionParams.Where(p => compressNames.Contains(p.name))) {
                param.SetNetworkSynced(false);
            }
            var newCost = expressionParams.CalcTotalCost();
            expressionParams.WorkLog(
                $"Compressed {decision.compress.Count} expression parameters to fit VRChat limits ({originalCost} bits to {newCost} bits)"
            );

            actionController.UpgradeWrongParamTypes();
            expressionParams.Dirty();
            CreateDebugInfo(decisionWithInfo, originalCost, newCost);
            saveAssetsService.Run(new[] { actionController });
        }

        private void CreateDebugInfo(ParameterCompressorSolverOutput decisionWithInfo, int originalCost, int newCost) {
            var options = decisionWithInfo.options;
            var decision = decisionWithInfo.decision;
            var types = options.FormatTypes();

            var paramWarnings = decisionWithInfo.FormatWarnings(100);

            var batchCount = decision.GetBatchCount();
            // account for the extra frame hack needed in the layer generator, which can add half a frame per batch. Assume 30fps.
            var minSyncTime = batchCount * (BATCH_TIME + (1 / 30f) * 0.5f);
            // Assume we just missed to the batch, so it has to do 2 full loops
            var maxSyncTime = minSyncTime * 2;

            string syncDelay;
            string legacyWarning = "";
            if (decision.useBadPriorityMethod) {
                syncDelay = $"Full sync time: {minSyncTime.ToString("N1")} seconds";
                legacyWarning =
                    "\n\nWARNING: The creator of this avatar has opted into the legacy compression system, which is worse for performance,"
                    + " wastes index bits, can sync params out of order (you may appear naked momentarily if toggles sync out of order), wastes a ton of FX parameters,"
                    + " and makes avatar load time worse. The only benefit of the legacy system is reduced delay for toggle sync in some situations.";
            } else {
                syncDelay = $"Sync delay: {minSyncTime.ToString("N1")} - {maxSyncTime.ToString("N1")} seconds";
            }

            var debug = avatarObject.AddComponent<VRCFuryDebugInfo>();
            debug.title = "Parameter Compressor";
            debug.debugInfo =
                "VRCFury compressed the parameters on this avatar to make them fit VRC's limit."
                + legacyWarning
                + $"\n\nOld Total: {FormatBitsPlural(originalCost)}"
                + $"\nNew Total: {FormatBitsPlural(newCost)}"
                + (!string.IsNullOrEmpty(types) ? $"\nCompressed types: {types}" : "")
                + $"\n{syncDelay}"
                + $"\nBools per batch: {decision.boolSlots}"
                + $"\nNumbers per batch: {decision.numberSlots}"
                + $"\nBatches per sync: {batchCount}"
                + (string.IsNullOrEmpty(paramWarnings) ? "" : $"\n\n{paramWarnings}");
            debug.warn = true;
        }

        public static string FormatBitsPlural(int numBits) {
            return numBits + " bit" + (numBits != 1 ? "s" : "");
        }
    }
}
