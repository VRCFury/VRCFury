using System.Collections.Generic;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * This can build the contacts needed for haptic component depth animations
     */
    [VFService]
    internal class HapticAnimContactsService {
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;

        public void CreateAnims(
            string layerName,
            ICollection<VRCFuryHapticSocket.DepthActionNew> actions,
            VFGameObject spsComponentOwner,
            string name,
            SpsDepthContacts contacts
        ) {
            var actionNum = 0;
            foreach (var depthAction in actions) {
                actionNum++;
                var prefix = $"{name}/Anim/{actionNum}";

                var unsmoothed = depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Plugs
                    ? ( depthAction.enableSelf ? contacts.closestDistancePlugLengths.Value : contacts.others.distancePlugLengths.Value )
                    : depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Meters
                    ? ( depthAction.enableSelf ? contacts.closestDistanceMeters.Value : contacts.others.distanceMeters.Value )
                    : ( depthAction.enableSelf ? contacts.closestDistanceLocal.Value : contacts.others.distanceLocal.Value );
                var dbt = dbtLayerService.Create($"{layerName} - {actionNum} - Action");
                var math = dbtLayerService.GetMath(dbt);
                var mapped = math.Map(
                    $"{prefix}/Mapped",
                    unsmoothed,
                    depthAction.range.Max(), depthAction.range.Min(),
                    0, 1
                );
                var smoothed = smoothing.Smooth(
                    dbt,
                    $"{prefix}/Smoothed",
                    mapped,
                    depthAction.smoothingSeconds
                );

                var layer = fx.NewLayer($"{layerName} - {actionNum} - Action");
                var off = layer.NewState("Off");
                var on = layer.NewState("On");

                var action = actionClipService.LoadStateAdv(prefix, depthAction.actionSet, spsComponentOwner, ActionClipService.MotionTimeMode.Auto);
                if (action.useMotionTime) {
                    on.WithAnimation(action.onClip).MotionTime(smoothed);
                    if (depthAction.reverseClip) {
                        foreach (var clip in new AnimatorIterator.Clips().From(action.onClip)) {
                            clip.Reverse();
                        }
                    }
                } else {
                    var tree = VFBlendTree1D.Create(prefix + " tree", smoothed);
                    tree.Add(0, clipFactory.GetEmptyClip());
                    tree.Add(1, action.onClip);
                    on.WithAnimation(tree);
                }

                if (depthAction.reverseClip) {
                    off.TransitionsTo(on).When(fx.Always());
                } else {
                    var onWhen = smoothed.IsGreaterThan(0.01f);
                    off.TransitionsTo(on).When(onWhen);
                    on.TransitionsTo(off).When(onWhen.Not());
                }
            }
        }
    }
}
