using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    /**
     * Builds the FX layer responsible for actually handling the parameter compression.
     */
    [VFService]
    internal class ParameterCompressorLayerService {
        [VFAutowired] private readonly CompressorLayerUtilsService layerUtilsService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();

        public void BuildLayer(OptimizationDecision decision) {
            var (numberBatches, boolBatches) = decision.GetBatches();

            var batchCount = Math.Max(numberBatches.Count, boolBatches.Count);
            var indexBitCount = decision.GetIndexBitCount();
            var syncIndex = Enumerable.Range(0, indexBitCount)
                .Select(i => fx.NewBool($"SyncIndex{i}", synced: true))
                .ToArray();
            var syncInts = Enumerable.Range(0, decision.numberSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataNum" + i, VRCExpressionParameters.ValueType.Int, true))
                .ToList();
            var syncBools = Enumerable.Range(0, decision.boolSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataBool" + i, VRCExpressionParameters.ValueType.Bool, true))
                .ToList();

            var layer = fx.NewLayer("Parameter Compressor");
            layer.weight = 0;
            var entry = layer.NewState("Entry").Move(0, 2);
            //entry.TransitionsFromAny().When(fx.IsAnimatorEnabled().IsFalse());
            var remoteLost = layer.NewState("Receive (Lost)").Move(entry, 0, 1);
            entry.TransitionsTo(remoteLost).When(fx.IsLocal().IsFalse());
            var local = layer.NewState("Local").Move(entry, -2, 1);
            entry.TransitionsTo(local).When(fx.Always());

            Action applyLatchDrivers = () => { };
            Action applyCopyDrivers = () => { };
            Action applyUnlatchDrivers = () => { };
            Action applyLoopTransition = () => { };
            Action<VFState,VFState,VFCondition> whenNextStateReady = null;
            VFState latchState = null;
            VFState unlatchState = null;
            var yOffset = 2;
            foreach (var batchNum in Enumerable.Range(0, batchCount)) {
                Action<VFState> sendIdDriver = s => { };

                var syncId = batchNum + 1;
                if (syncId >= (1 << indexBitCount)) throw new Exception("Unexpected syncId outside of index bit range");
                var syncIds = Enumerable.Range(0, indexBitCount).Select(i => (syncId & 1<<(indexBitCount-1-i)) > 0).ToArray();
                var titleId = syncIds.Select(b => b ? "1" : "0").Join("");
                var receiveConditions = new List<VFCondition>();
                foreach (var i in Enumerable.Range(0, indexBitCount)) {
                    sendIdDriver += s => s.Drives(syncIndex[i], syncIds[i]);
                    receiveConditions.Add(syncIndex[i].Is(syncIds[i]));
                }
                var receiveCondition = VFCondition.All(receiveConditions);

                var numberBatch = batchNum < numberBatches.Count ? numberBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();
                var boolBatch = batchNum < boolBatches.Count ? boolBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();

                // What is this sync index called?
                var title = $"({titleId}):\n" + numberBatch.Concat(boolBatch).Select(p => p.name).Join("\n");

                var latchNow = batchNum == 0;
                var unlatchNow = batchNum == batchCount - 1;

                var sendState = layer.NewState($"{(latchNow ? "Latch & " : "")}Send {title}").Move(entry, -1, yOffset);
                var receiveState = layer.NewState($"Receive{(unlatchNow ? " & Unlatch" : "")} {title}").Move(entry, 1, yOffset);
                
                // We can't just go to the next send after 0.1s, because of a weird unity animator quirk where it will exit
                // "early" if it thinks the exit time is closer to the current frame than the next frame, which would potentially
                // make it update faster than the sync rate and lose a packet. To fix this, we have to add exactly one extra frame by going through
                // an additional state between each send.
                var sendStateExtraFrame = layer.NewState("Extra Frame").Move(entry, -2, yOffset);

                if (batchNum == 0) {
                    local.TransitionsTo(sendState).When(fx.Always());
                    remoteLost.TransitionsTo(receiveState).When(receiveCondition);
                    applyLoopTransition += () => {
                        whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                    };
                } else {
                    whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                }
                whenNextStateReady = (nextSend, nextRecv, nextRecvCond) => {
                    sendState.TransitionsTo(nextSend).WithTransitionExitTime(ParameterCompressorService.BATCH_TIME).When();
                    receiveState.TransitionsTo(nextRecv).When(nextRecvCond);
                    receiveState.TransitionsTo(remoteLost).When(receiveCondition.Not().And(nextRecvCond.Not()));
                };
                if (latchNow) latchState = sendState;
                if (unlatchNow) unlatchState = receiveState;

                sendIdDriver.Invoke(sendState);
                sendStateExtraFrame.TransitionsTo(sendState).When(fx.Always());

                void SyncParam(VRCExpressionParameters.Parameter original, VRCExpressionParameters.Parameter slot) {
                    VRCExpressionParameters.Parameter sendFrom;
                    VRCExpressionParameters.Parameter receiveTo;
                    var latch = new Lazy<VRCExpressionParameters.Parameter>(() => layerUtilsService.MakeParam(original.name, original.valueType, false));
                    if (latchNow) {
                        // No need to latch things in the first batch when sending
                        sendFrom = original;
                    } else {
                        applyLatchDrivers += () => latchState?.DrivesCopy(original.name, latch.Value.name);
                        sendFrom = latch.Value;
                    }
                    if (unlatchNow) {
                        receiveTo = original;
                    } else {
                        applyUnlatchDrivers += () => unlatchState?.DrivesCopy(latch.Value.name, original.name);
                        receiveTo = latch.Value;
                    }
                    if (original.valueType == VRCExpressionParameters.ValueType.Float) {
                        applyCopyDrivers += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name, -1, 1, 0, 254);
                            receiveState.DrivesCopy(slot.name, receiveTo.name, 0, 254, -1, 1);
                        };
                    } else if (original.valueType == VRCExpressionParameters.ValueType.Int || original.valueType == VRCExpressionParameters.ValueType.Bool) {
                        applyCopyDrivers += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name);
                            receiveState.DrivesCopy(slot.name, receiveTo.name);
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
                
                yOffset += (int)Math.Ceiling((title.Split('\n').Length+1) / 5f);
            }
            applyLoopTransition.Invoke();
            applyLatchDrivers.Invoke();
            applyCopyDrivers.Invoke();
            applyUnlatchDrivers.Invoke();
            layerUtilsService.FixWd(layer);
        }
    }
}
