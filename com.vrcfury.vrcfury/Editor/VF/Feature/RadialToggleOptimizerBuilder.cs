
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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
    public class RadialToggleOptimizerBuilder : FeatureBuilder<RadialToggleOptimizer> {
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly SmoothingService smoothingService;
        
        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        [FeatureBuilderAction(FeatureOrder.RadialToggleOptimizer)]
        public void Apply() {
            if (networkSyncedField == null) return;
            var toOptimize = GetAllSyncedRadialPuppetParameters();
            if (toOptimize.Count <= 2) return; // don't optimize 16 bits or less
            var fx = GetFx();
            var layers = fx.GetLayers();
            foreach (var param in toOptimize) {
                var vrcPrm = manager.GetParams().GetParam(param.Name());
                networkSyncedField.SetValue(vrcPrm, false);
            }
            var pointer = fx.NewInt("SyncPointer", synced: true);
            var localPointer = fx.NewInt("LocalPointer"); // Use a local only pointer to avoid race condition
            var data = fx.NewFloat("SyncData", synced: true);
            //var sending = fx.NewFloat("IsSending");

            var layer = fx.NewLayer("Radial Toggle Optimizer");
            var idle = layer.NewState("Idle");
            //txIdle.Drives(sending, 0, true);
            var local = layer.NewState("Local");
            var remote = layer.NewState("Remote");
            idle.TransitionsTo(local)
                .When(fx.IsLocal().IsTrue());
            idle.TransitionsTo(remote)
                .When(fx.IsLocal().IsFalse());
            var txAdder = layer.NewState("Adder");
            var txResetter = layer.NewState("Reset");
            txResetter.Drives(localPointer, 0, local: true)
                .TransitionsToExit()
                .When(fx.Always());
            txAdder.TransitionsTo(txResetter)
                .When(localPointer.IsGreaterThan(toOptimize.Count - 1)); // always > 0
            txAdder.DrivesDelta(localPointer, 1)
                .TransitionsToExit()
                .When(fx.Always());

            var sending = toOptimize.Select((_, i) => fx.NewFloat($"sending{i}")).ToList();
            // instant sync on change detection
            for (int i = 0; i < toOptimize.Count; i++) {
                var src = toOptimize[i];
                var lastSrc = math.Buffer(src);
                var srcDiff = math.Subtract(src, lastSrc);
                
                var pending = math.SetValueWithConditions($"diffPending{i}",
                    (0, math.GreaterThan(sending[i], 0.5f)),
                    (1, math.Or(math.GreaterThan(srcDiff, 0), math.LessThan(srcDiff, 0))));
                /*var maintain = math.MakeMaintainer(srcDiff); // Maintain while in send animation
                directTree.Add(sending, maintain);*/
                var sendInstantPrepareState = layer.NewState($"Send Prepare Instant {toOptimize[i].Name()}");
                sendInstantPrepareState.Drives(sending[i], 1, true);
                var sendInstantState = layer.NewState($"Send Instant {toOptimize[i].Name()}");
                sendInstantState
                    .DrivesCopy(data, src, true)
                    .Drives(pointer, i, true)
                    .Drives(sending[i], 0, true)
                    .TransitionsToExit()
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                local.TransitionsTo(sendInstantPrepareState)
                    .When(pending.IsGreaterThan(0.5f)); // srcDiff.IsGreaterThan(0).Or(srcDiff.IsLessThan(0))));
                sendInstantPrepareState.TransitionsTo(sendInstantState)
                    .When(fx.Always());
                //txIdle.Drives(sending[i], 0, true);
            }

            // round robin pointer iteration
            for (int i = 0; i < toOptimize.Count; i++) {
                var src = toOptimize[i];
                var sendPrepareState = layer.NewState($"Send Prepare {toOptimize[i].Name()}");
                sendPrepareState.Drives(sending[i], 1, true);
                var sendState = layer.NewState($"Send {toOptimize[i].Name()}");
                sendState
                    .DrivesCopy(data, src)
                    .DrivesCopy(pointer, localPointer)
                    .Drives(sending[i], 0, true)
                    .TransitionsTo(txAdder)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                local.TransitionsTo(sendPrepareState)
                    .When(fx.IsLocal().IsTrue().And(localPointer.IsEqualTo(i)));
                sendPrepareState.TransitionsTo(sendState)
                    .When(fx.Always());
            }
            
            //var rxLayer = fx.NewLayer("Receive");
            //var rxIdle = rxLayer.NewState("Idle");
            var smoothedDict = new Dictionary<string, string>();
            for (int i = 0; i < toOptimize.Count; i++) {
                var dst = toOptimize[i];
                var rxState = layer.NewState($"Receive {toOptimize[i].Name()}");
                rxState.DrivesCopy(dst, data, false)
                    .TransitionsToExit()
                    .When(fx.Always());
                remote.TransitionsTo(rxState)
                    .When(pointer.IsEqualTo(i));
                
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

        private List<VFAFloat> GetAllSyncedRadialPuppetParameters() {
            var floatParams = new List<VFAFloat>();
            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var controlParam = control.GetSubParameter(0)?.name;
                if(string.IsNullOrEmpty(controlParam)) return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var vrcParam = manager.GetParams().GetParam(controlParam);
                if (vrcParam == null || vrcParam.networkSynced == false)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var animParam = GetFx().GetRaw().GetParam(control.GetSubParameter(0).name);
                if(animParam != null && animParam.type == AnimatorControllerParameterType.Float) 
                    floatParams.Add(new VFAFloat(animParam));
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return floatParams;
        }
        
        public override string GetEditorTitle() {
            return "Radial Toggle Optimizer";
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