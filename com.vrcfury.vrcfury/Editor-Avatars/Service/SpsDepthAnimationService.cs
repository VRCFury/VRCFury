using static VF.Inspector.VRCFuryHapticPlugEditor;
using System;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System.Collections.Generic;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    // Builds depth and radius animations from shared SPS measurements.
    [VFService]
    internal class SpsDepthAnimationService {
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ObjectTreeService objectTrees;

        public void CreateAnims(
            IEnumerable<VRCFuryHapticSocket.DepthActionNew> actions,
            VFGameObject spsComponentOwner,
            string name,
            Func<Party, Func<string, VFAParam>> getMeasurements,
            VFGameObject bakeRoot,
            string measurement = SpsParamDepth
        ) {
            var calculations = objectTrees.Get(bakeRoot);
            var increasing = measurement == SpsParamPlugEntryRadius;
            var controls = new Dictionary<(string input, float off, float on, float seconds, float initial), VFAParam>();
            var actionNum = 0;
            foreach (var depthAction in actions) {
                var reverseClip = !increasing && depthAction.reverseClip;
                actionNum++;
                var prefix = $"{name}/Anim/{actionNum}";

                var measurements = getMeasurements(depthAction.enableSelf ? Party.Merged : Party.Others);
                var units = depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? SpsUnitsPlugLengths
                    : depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Meters ? SpsUnitsMeters : SpsUnitsLocal;
                var unsmoothed = measurements(GetSpsMagicParam(measurement, units));
                var action = actionClipService.LoadStateAdv(prefix, depthAction.actionSet, spsComponentOwner, ActionClipService.MotionTimeMode.Auto);
                var useRawInput = depthAction.smoothingSeconds <= 0 && !action.useMotionTime
                    && depthAction.range.Min() < depthAction.range.Max();
                var parameter = unsmoothed;
                var offThreshold = increasing ? depthAction.range.Min() : depthAction.range.Max();
                var onThreshold = increasing ? depthAction.range.Max() : depthAction.range.Min();
                if (!useRawInput) {
                    var initial = action.useMotionTime && reverseClip ? 1f : 0f;
                    var seconds = Math.Max(0, Math.Min(10, depthAction.smoothingSeconds));
                    parameter = controls.GetOrCreate((unsmoothed.name, offThreshold, onThreshold, seconds, initial), () =>
                        smoothing.Smooth(calculations, prefix + (seconds > 0 ? "/Smoothed" : "/Mapped"), seconds, initial,
                            output => BlendtreeMath.Map(output, unsmoothed, offThreshold, onThreshold, 0, 1),
                            minSupported: 0, maxSupported: 1));
                    offThreshold = 0;
                    onThreshold = 1;
                }

                if (!action.useMotionTime) {
                    var tree = VFBlendTree1D.Create(prefix + " tree", parameter);
                    if (useRawInput && !increasing) {
                        tree.Add(onThreshold, action.onClip);
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                    } else {
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                        tree.Add(onThreshold, action.onClip);
                    }
                    var active = useRawInput
                        ? increasing
                            ? BlendtreeMath.GreaterThan(parameter, offThreshold + (onThreshold - offThreshold) * 0.01f)
                            : BlendtreeMath.LessThan(parameter, offThreshold + (onThreshold - offThreshold) * 0.01f)
                        : BlendtreeMath.GreaterThan(parameter, 0.01f);
                    calculations.Add(reverseClip ? tree : active.create(tree, null));
                    continue;
                }
                var layer = fx.NewLayer($"{name} - {(increasing ? "Plug Entry Radius" : "Depth")} Animations - {actionNum} - Action");
                var off = layer.NewState("Off");
                var on = layer.NewState("On");
                on.WithAnimation(action.onClip).MotionTime(parameter);
                if (reverseClip) {
                    foreach (var clip in new AnimatorIterator.Clips().From(action.onClip)) clip.Reverse();
                }

                if (reverseClip) {
                    off.TransitionsTo(on).When(fx.Always());
                } else {
                    var onWhen = parameter.IsGreaterThan(0.01f);
                    off.TransitionsTo(on).When(onWhen);
                    on.TransitionsTo(off).When(onWhen.Not());
                }
            }
        }
    }
}
