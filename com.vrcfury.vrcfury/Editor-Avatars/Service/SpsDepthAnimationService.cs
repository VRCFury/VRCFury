using static VF.Inspector.VRCFuryHapticPlugEditor;
using System;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System.Collections.Generic;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    // Builds depth animations from shared SPS measurements.
    [VFService]
    internal class SpsDepthAnimationService {
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ObjectTreeService objectTrees;

        public void CreateAnims(
            ICollection<VRCFuryHapticSocket.DepthActionNew> actions,
            VFGameObject spsComponentOwner,
            string name,
            Func<Party, Func<string, VFAParam>> getMeasurements,
            VFGameObject bakeRoot
        ) {
            var calculations = objectTrees.Get(bakeRoot);
            var math = new BlendtreeMath(fx, calculations);
            var actionNum = 0;
            foreach (var depthAction in actions) {
                actionNum++;
                var prefix = $"{name}/Anim/{actionNum}";

                var measurements = getMeasurements(depthAction.enableSelf ? Party.Merged : Party.Others);
                var units = depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? SpsUnitsPlugLengths
                    : depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Meters ? SpsUnitsMeters : SpsUnitsLocal;
                var unsmoothed = measurements(GetSpsMagicParam(SpsParamDepth, units));
                var action = actionClipService.LoadStateAdv(prefix, depthAction.actionSet, spsComponentOwner, ActionClipService.MotionTimeMode.Auto);
                var useRawDepth = depthAction.smoothingSeconds <= 0 && !action.useMotionTime
                    && depthAction.range.Min() < depthAction.range.Max();
                var parameter = unsmoothed;
                var offThreshold = depthAction.range.Max();
                var onThreshold = depthAction.range.Min();
                if (!useRawDepth) {
                    var mapped = math.Map($"{prefix}/Mapped", unsmoothed, offThreshold, onThreshold, 0, 1);
                    mapped.SetDefault(action.useMotionTime && depthAction.reverseClip ? 1 : 0);
                    parameter = smoothing.Smooth(calculations, $"{prefix}/Smoothed", mapped, depthAction.smoothingSeconds);
                    offThreshold = 0;
                    onThreshold = 1;
                }

                if (!action.useMotionTime) {
                    var tree = VFBlendTree1D.Create(prefix + " tree", parameter);
                    if (useRawDepth) {
                        tree.Add(onThreshold, action.onClip);
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                    } else {
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                        tree.Add(onThreshold, action.onClip);
                    }
                    var active = useRawDepth
                        ? BlendtreeMath.LessThan(parameter, offThreshold + (onThreshold - offThreshold) * 0.01f)
                        : BlendtreeMath.GreaterThan(parameter, 0.01f);
                    calculations.Add(depthAction.reverseClip ? tree : active.create(tree, null));
                    continue;
                }
                var layer = fx.NewLayer($"{name} - Depth Animations - {actionNum} - Action");
                var off = layer.NewState("Off");
                var on = layer.NewState("On");
                on.WithAnimation(action.onClip).MotionTime(parameter);
                if (depthAction.reverseClip) {
                    foreach (var clip in new AnimatorIterator.Clips().From(action.onClip)) clip.Reverse();
                }

                if (depthAction.reverseClip) {
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
