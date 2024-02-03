using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    public class OnDemandSyncService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly SmoothingService smoothingService;
        
        private readonly List<VFAFloat> onDemandSyncParameters = new List<VFAFloat>();
        
        public void SetParameterOnDemandSync(VFAFloat parameter) {
            onDemandSyncParameters.Add(parameter);
        }

        [FeatureBuilderAction(FeatureOrder.OnDemandSync)]
        public void Apply() {
            if (onDemandSyncParameters.Count == 0) return;
            
            var fx = manager.GetController(VRCAvatarDescriptor.AnimLayerType.FX);
            var layers = fx.GetRaw().GetLayers();
            foreach (var param in onDemandSyncParameters) {
                manager.GetParams().GetParam(param.Name()).networkSynced = false;
            }
            var pointer = fx.NewInt("SyncPointer", synced: true);
            var localPointer = fx.NewInt("LocalPointer"); // Use a local only pointer to avoid race condition
            var data = fx.NewFloat("SyncData", synced: true);
            var sending = fx.NewFloat("IsSending");

            var txLayer = fx.NewLayer("Send");
            var txIdle = txLayer.NewState("Idle");
            txIdle.Drives(sending, 0, true);
            var txAdder = txLayer.NewState("Adder");
            var txResetter = txLayer.NewState("Reset");
            txResetter.Drives(localPointer, 0, local: true)
                .TransitionsToExit()
                .When(fx.Always());
            txAdder.TransitionsTo(txResetter)
                .When(localPointer.IsGreaterThan(onDemandSyncParameters.Count - 1)); // always > 0
            txAdder.DrivesDelta(localPointer, 1)
                .TransitionsToExit()
                .When(fx.Always());
            
            // instant sync on change detection
            for (int i = 0; i < onDemandSyncParameters.Count; i++) {
                var src = onDemandSyncParameters[i];
                var lastSrc = fx.NewFloat($"last{src.Name()}");
                directTree.Add(math.MakeCopier(src, lastSrc)); 
                var srcDiff = math.Subtract(src, lastSrc);
                var maintain = math.MakeMaintainer(srcDiff); // Maintain while in send animation
                directTree.Add(sending, maintain);
                
                var sendInstantState = txLayer.NewState($"Send Instant {onDemandSyncParameters[i]}");
                sendInstantState
                    .DrivesCopy(data, src)
                    .Drives(pointer, i)
                    .Drives(sending, 1, true)
                    .TransitionsToExit()
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                txIdle.TransitionsTo(sendInstantState)
                    .When(fx.IsLocal().IsTrue().And(srcDiff.IsGreaterThan(0).Or(srcDiff.IsLessThan(0))));
            }

            // round robin pointer iteration
            for (int i = 0; i < onDemandSyncParameters.Count; i++) {
                var src = onDemandSyncParameters[i];
                var sendState = txLayer.NewState($"Send {onDemandSyncParameters[i]}");
                sendState
                    .DrivesCopy(data, src)
                    .DrivesCopy(pointer, localPointer)
                    .Drives(sending, 1, true)
                    .TransitionsTo(txAdder)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                txIdle.TransitionsTo(sendState)
                    .When(fx.IsLocal().IsTrue().And(localPointer.IsEqualTo(i)));
            }
            
            var rxLayer = fx.NewLayer("Receive");
            var rxIdle = rxLayer.NewState("Idle");
            var smoothedDict = new Dictionary<string, string>();
            for (int i = 0; i < onDemandSyncParameters.Count; i++) {
                var dst = onDemandSyncParameters[i];
                var rxState = rxLayer.NewState($"Receive {onDemandSyncParameters[i]}");
                rxState.DrivesCopy(dst, data, false)
                    .TransitionsToExit()
                    .When(fx.Always());
                rxIdle.TransitionsTo(rxState)
                    .When(pointer.IsEqualTo(i).And(fx.IsLocal().IsFalse()));
                
                var dstSmoothed = smoothingService.Smooth($"{dst.Name()}/Smoothed", dst, 0.1f, false);
                smoothedDict[dst.Name()] = dstSmoothed.Name();
            }
            fx.GetRaw().RewriteParameters(name => {
                if (smoothedDict.TryGetValue(name, out var smoothed)) {
                    return smoothed;
                }
                return name;
            }, false, layers.Select(l => l.stateMachine).ToArray());
        }
    }
}