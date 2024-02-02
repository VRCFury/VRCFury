using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils.Controller;

namespace Editor.VF.Feature {
    public class OnDemandSyncBuilder : FeatureBuilder<OnDemandSync> {
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly FrameTimeService frameTimeService;
        [VFAutowired] private readonly SmoothingService smoothingService;
        
        [FeatureBuilderAction()]
        public void Apply() {
            var fx = GetFx();
            var layers = fx.GetRaw().GetLayers();
            foreach (var param in model.parameters) {
                manager.GetParams().GetParam(param).networkSynced = false;
            }
            var pointer = fx.NewInt("SyncPointer", synced: true);
            var localPointer = fx.NewInt("LocalPointer"); // Use a local only pointer to avoid race condition
            var data = fx.NewFloat("SyncData", synced: true);
            
            var txLayer = fx.NewLayer("Send");
            var txIdle = txLayer.NewState("Idle");
            var txAdder = txLayer.NewState("Adder");
            var txResetter = txLayer.NewState("Reset");
            txResetter.Drives(localPointer, 0, local: true)
                .TransitionsToExit()
                .When(fx.Always());
            txAdder.TransitionsTo(txResetter)
                .When(localPointer.IsGreaterThan(model.parameters.Count - 1));
            txAdder.DrivesDelta(localPointer, 1)
                .TransitionsToExit()
                .When(fx.Always());
            for (int i = 0; i < model.parameters.Count; i++) {
                var src = new VFAFloat(fx.GetRaw().GetParam(model.parameters[i]));
                var sendState = txLayer.NewState($"Send {model.parameters[i]}");
                sendState
                    .DrivesCopy(data, src)
                    .DrivesCopy(pointer, localPointer)
                    .TransitionsTo(txAdder)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                txIdle.TransitionsTo(sendState)
                    .When(pointer.IsEqualTo(i).And(fx.IsLocal().IsTrue()));
            }
            
            var rxLayer = fx.NewLayer("Receive");
            var rxIdle = rxLayer.NewState("Idle");
            var smoothedDict = new Dictionary<string, string>();
            for (int i = 0; i < model.parameters.Count; i++) {
                var dst = new VFAFloat(fx.GetRaw().GetParam(model.parameters[i]));
                var rxState = rxLayer.NewState($"Receive {model.parameters[i]}");
                rxState.DrivesCopy(dst, data, false)
                    .TransitionsToExit()
                    .When(fx.Always());
                rxIdle.TransitionsTo(rxState)
                    .When(pointer.IsEqualTo(i).And(fx.IsLocal().IsFalse()));
                
                var dstSmoothed = smoothingService.Smooth($"{dst.Name()}/Smoothed", dst, 0.1f, false);
                smoothedDict[dst.Name()] = dstSmoothed.Name();
                fx.GetRaw().RewriteParameters(name => {
                    if (smoothedDict.TryGetValue(name, out var smoothed)) {
                        return smoothed;
                    }
                    return name;
                }, false, layers.Select(l => l.stateMachine).ToArray());
            }
        }
        
        public override string GetEditorTitle() {
            return "On Demand Sync";
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("parameters")));
            return content;
        }
    }
}