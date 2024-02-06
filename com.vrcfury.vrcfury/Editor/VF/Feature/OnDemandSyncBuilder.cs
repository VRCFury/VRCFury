using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Editor.VF.Feature {
    public class OnDemandSyncBuilder : FeatureBuilder<OnDemandSync> {
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly SmoothingService smoothingService;

        [FeatureBuilderAction(FeatureOrder.OnDemandSync)]
        public void Apply() {
            var onDemandSyncParameters = GetAllRadialPuppetParameters();
            if (onDemandSyncParameters.Count == 0) return;
            var fx = GetFx();
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

        private List<VFAFloat> GetAllRadialPuppetParameters() {
            var floatParams = new List<VFAFloat>();
            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var controlParam = control.GetSubParameter(0).name;
                var vrcParam = manager.GetParams().GetParam(controlParam);
                if (vrcParam == null || vrcParam.networkSynced == false)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var animParam = GetFx().GetRaw().GetParam(control.GetSubParameter(0).name);
                if(animParam != null) floatParams.Add(new VFAFloat(animParam));
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return floatParams;
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
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will optimize all synced float parameters on radial toggles into a single" +
                " 16 bits pointer and data field combination, to sync the parameters on change."));
            return content;
        }
    }
}