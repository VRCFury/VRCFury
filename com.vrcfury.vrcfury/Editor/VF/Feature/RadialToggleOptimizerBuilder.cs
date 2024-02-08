using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Toggle = VF.Model.Feature.Toggle;

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
            var sending = fx.NewFloat("IsSending");
            var isLocalFloat = fx.NewFloat("IsLocalFloat");
            var instant = fx.NewBool("InstantSync", synced: true);
            var instantFloat = fx.NewFloat("InstantSyncFloat");

            var layer = fx.NewLayer("Radial Toggle Optimizer");
            var idle = layer.NewState("Idle");
            var local = layer.NewState("Local");
            local.Drives(sending, 0, true)
                .Drives(isLocalFloat, 1, true); // cast IsLocal to float to use in DBT
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

            // instant sync on change detection
            for (int i = 0; i < toOptimize.Count; i++) {
                var src = toOptimize[i];
                var lastSrc = math.Buffer(src);
                var srcDiff = math.Subtract(src, lastSrc);
                var pending = math.SetValueWithConditions($"diffPending{i}",
                    (1, math.Or(math.GreaterThan(srcDiff, 0), math.LessThan(srcDiff, 0))),
                    (0, null));
                var maintain = math.MakeMaintainer(pending); // Maintain while in send animation
                directTree.Add(sending, maintain);

                var sendInstantState = layer.NewState($"Send Instant {toOptimize[i].Name()}");
                sendInstantState
                    .DrivesCopy(data, src, true)
                    .Drives(pointer, i, true)
                    .Drives(sending, 1, true)
                    .Drives(instant, true, true)
                    .TransitionsToExit()
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                local.TransitionsTo(sendInstantState)
                    .When(pending.IsGreaterThan(0.5f));
            }

            // round robin pointer iteration
            for (int i = 0; i < toOptimize.Count; i++) {
                var src = toOptimize[i];
                var sendState = layer.NewState($"Send {toOptimize[i].Name()}");
                sendState
                    .DrivesCopy(data, src)
                    .DrivesCopy(pointer, localPointer)
                    .Drives(sending, 1, true)
                    .Drives(instant, false, true)
                    .TransitionsTo(txAdder)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                local.TransitionsTo(sendState)
                    .When(localPointer.IsEqualTo(i));
            }

            var mappedDict = new Dictionary<string, string>();
            for (int i = 0; i < toOptimize.Count; i++) {
                var dst = toOptimize[i];
                var rxState = layer.NewState($"Receive {toOptimize[i].Name()}");
                rxState.DrivesCopy(dst, data, false)
                    .DrivesCopy(instantFloat, instant, false)
                    .TransitionsToExit()
                    .When(fx.Always());
                remote.TransitionsTo(rxState)
                    .When(pointer.IsEqualTo(i));

                var dstSmoothed = smoothingService.Smooth($"{dst.Name()}/Smoothed", dst, 0.1f, false);
                var dstMapped = math.SetValueWithConditions($"{dst.Name()}/Mapped",
                    (dst, math.Or(math.GreaterThan(isLocalFloat, 0.5f), math.LessThan(instantFloat, 0.5f))),
                    (dstSmoothed, null));
                mappedDict[dst.Name()] = dstMapped.Name();
            }

            fx.GetRaw().RewriteParameters(name => {
                if (mappedDict.TryGetValue(name, out var mapped)) {
                    return mapped;
                }

                return name;
            }, false, layers.Select(l => l.stateMachine).ToArray());
            Debug.Log($"Radial Toggle Optimizer: Reduced {toOptimize.Count * 8} bits into 16 bits.");
        }


        private List<VFAFloat> GetAllSyncedRadialPuppetParameters() {
            var floatParams = new List<VFAFloat>();
            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var controlParam = control.GetSubParameter(0)?.name;
                if (string.IsNullOrEmpty(controlParam))
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var vrcParam = manager.GetParams().GetParam(controlParam);
                if (vrcParam == null || vrcParam.networkSynced == false)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var animParam = GetFx().GetRaw().GetParam(control.GetSubParameter(0).name);
                if (animParam != null && animParam.type == AnimatorControllerParameterType.Float)
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

        private HashSet<string> GetFloatParameters(VRCExpressionParameters vrcPrms) {
            if (vrcPrms == null) return new HashSet<string>();
            return vrcPrms.parameters.Where(prm =>
                    prm.networkSynced && prm.valueType == VRCExpressionParameters.ValueType.Float)
                .Select(prm => prm.name)
                .ToHashSet();
        }

        private int CountRadials(VRCExpressionsMenu menu, HashSet<string> floatPrms) {
            if (menu == null) return 0;
            var radialPrms = new HashSet<string>();
            menu.ForEachMenu(ForEachItem: (control, list) => {
                if (control.type != VRCExpressionsMenu.Control.ControlType.RadialPuppet)
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                var prm = control.GetSubParameter(0)?.name;
                if (string.IsNullOrEmpty(prm)) return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                if (floatPrms.Contains(prm)) radialPrms.Add(prm);
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            return radialPrms.Count;
        }

        private int CountRadialToggles() {
            var features = avatarObject.GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(vrcf => vrcf.config.features).ToList();

            var toggleSliders = features.OfType<Toggle>().Count(toggle => toggle.slider);

            var fullControllerRadials = features.OfType<FullController>().Sum(fullController => {
                var prms = fullController.prms.SelectMany(prm => GetFloatParameters(prm.parameters.Get())).ToHashSet();
                var radials = fullController.menus
                    .Sum(menu => CountRadials(menu.menu.Get(), prms));
                return radials;
            });

            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            var vrcPrms = GetFloatParameters(avatar.expressionParameters);
            var vrcRadials = CountRadials(avatar.expressionsMenu, vrcPrms);

            return vrcRadials + toggleSliders + fullControllerRadials;
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will optimize all synced float parameters on radial toggles into a single" +
                " 16 bits pointer and data field combination, to sync the parameters on change."));
            content.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                if (avatarObject == null) return "Avatar descriptor is missing";
                var radials = CountRadialToggles();
                var parameterSpace = radials * 8;
                if (radials <= 2) {
                    return
                        $"Only {radials} have been found. Optimization applies when 3 or more radial toggles are used";
                } else {
                    return $"{parameterSpace} bits of float parameters are optimized into 16 bits.";
                }
            }));
            return content;
        }
    }
}