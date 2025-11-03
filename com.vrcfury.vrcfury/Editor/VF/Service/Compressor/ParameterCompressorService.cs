using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using VF.Builder;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Utils;

namespace VF.Service.Compressor {
    /**
     * Main entrypoint for compressing parameters to get them under the VRC limit
     */
    [VFService]
    internal class ParameterCompressorService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ParameterCompressorSolverService solverService;
        [VFAutowired] private readonly ParameterCompressorLayerService newLayerService;
        [VFAutowired] private readonly ParameterCompressorLegacyLayerService legacyLayerService;
        [VFAutowired] private readonly ParameterPlatformAlignmentService platformAlignmentService;

        public const float BATCH_TIME = 0.1f;

        public void Apply() {
            ParameterCompressorSolverOutput decisionWithInfo;
            {
                // This is written weird to make sure we don't clone the params if we don't have to
                var readOnlyParams = paramsService.GetReadOnlyParams();
                var mutated = readOnlyParams.Clone();
                mutated.RemoveDuplicates();
                if (BuildTargetUtils.IsDesktop()) {
                    decisionWithInfo = solverService.GetParamsToOptimize();
                    platformAlignmentService.SaveToDiskAfterBuild(decisionWithInfo.decision, mutated);
                } else {
                    decisionWithInfo = platformAlignmentService.AlignForMobile(mutated);
                    if (decisionWithInfo == null) {
                        decisionWithInfo = solverService.GetParamsToOptimize();
                    }
                }
                if (!readOnlyParams.IsSameAs(mutated)) {
                    paramsService.GetParams().GetRaw().parameters = mutated.parameters;
                }
            }

            var decision = decisionWithInfo.decision;
            if (!decision.compress.Any()) return;

            var paramz = paramsService.GetParams().GetRaw();
            var originalCost = paramz.CalcTotalCost();

            if (decision.useBadPriorityMethod) {
                legacyLayerService.BuildLayer(decision);
            } else {
                newLayerService.BuildLayer(decision);
            }
            
            var compressNames = decision.compress.Select(p => p.name).ToImmutableHashSet();
            foreach (var param in paramz.parameters.Where(p => compressNames.Contains(p.name))) {
                param.SetNetworkSynced(false);
            }
            var newCost = paramz.CalcTotalCost();

            NoBadControllerParamsService.UpgradeWrongParamTypes(fx);
            // Hopefully temporary until we can work out a better "re-save and/or re-dirty everything in a build hook at the end of the build" system
            VRCFuryEditorUtils.MarkDirty(fx.GetRaw());
            VRCFuryEditorUtils.MarkDirty(paramz);
            CreateDebugInfo(decisionWithInfo, originalCost, newCost);
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
