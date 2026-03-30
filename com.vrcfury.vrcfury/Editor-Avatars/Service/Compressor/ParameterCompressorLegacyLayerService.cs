using System;
using System.Collections.Generic;
using System.Linq;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    /**
     * Builds the FX layer responsible for actually handling the parameter compression.
     * This one uses the legacy "priority" method, which is really expensive on transitions,
     * doesn't work properly with interlinked params, costs a ton of extra fx param slots,
     * uses more synced bits for indexing, and is overall just bad. The only benefit
     * is that changed parameters MIGHT sync faster. Sometimes.
     */
    [VFService]
    internal class ParameterCompressorLegacyLayerService {
        [VFAutowired] private readonly CompressorLayerUtilsService layerUtilsService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        public void BuildLayer(OptimizationDecision decision) {
            var (numberBatches, boolBatches) = decision.GetBatches();

            var syncPointer = fx.NewInt("SyncPointer", synced: true);
            var syncInts = Enumerable.Range(0, decision.numberSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataNum" + i, VRCExpressionParameters.ValueType.Int, true))
                .ToList();
            var syncBools = Enumerable.Range(0, decision.boolSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataBool" + i, VRCExpressionParameters.ValueType.Bool, true))
                .ToList();

            var layer = fx.NewLayer("Legacy Parameter Compressor");
            layer.weight = 0;
            var entry = layer.NewState("Entry").Move(-3, -1);
            var local = layer.NewState("Local").Move(0, 2);
            entry.TransitionsTo(local).When(fx.IsLocal().IsTrue());
            entry.TransitionsToExit().When(fx.Always());

            var directLayer = fx.NewLayer("Legacy Parameter Compressor (Math)");
            var tree = VFBlendTreeDirect.Create("DBT");
            directLayer.NewState("DBT").WithAnimation(tree);
            var math = new BlendtreeMath(fx, tree);

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
                    .WithTransitionExitTime(ParameterCompressorService.BATCH_TIME)
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
                        sendState.DrivesCopy(originalParam, syncInts[slotNum].name, -1, 1, 0, 254);
                        receiveState.DrivesCopy(syncInts[slotNum].name, originalParam, 0, 254, -1, 1);
                    } else if (type == VRCExpressionParameters.ValueType.Int) {
                        currentValue = fx.NewFloat($"{originalParam}/Current");
                        local.DrivesCopy(originalParam, currentValue);
                        sendState.DrivesCopy(originalParam, syncInts[slotNum].name);
                        receiveState.DrivesCopy(syncInts[slotNum].name, originalParam);
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
                    sendState.DrivesCopy(originalParam, syncBools[slotNum].name);
                    receiveState.DrivesCopy(syncBools[slotNum].name, originalParam);
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
            layerUtilsService.FixWd(layer);
        }
    }
}
