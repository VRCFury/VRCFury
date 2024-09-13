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
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly AvatarManager avatarManager;
        private ControllerManager fx => avatarManager.GetFx();
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public void CreateAnims(
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
                var mapped = math.Map(
                    $"{prefix}/Mapped",
                    unsmoothed,
                    depthAction.range.Max(), depthAction.range.Min(),
                    0, 1
                );
                var smoothed = smoothing.Smooth(
                    $"{prefix}/Smoothed",
                    mapped,
                    depthAction.smoothingSeconds
                );

                var layer = fx.NewLayer("Depth Animation " + actionNum + " for " + name);
                var off = layer.NewState("Off");
                var on = layer.NewState("On");

                var action = actionClipService.LoadStateAdv(prefix, depthAction.actionSet, spsComponentOwner, ActionClipService.MotionTimeMode.Auto);
                if (action.useMotionTime) {
                    on.WithAnimation(action.onClip).MotionTime(smoothed);
                    if (depthAction.reverseClip) {
                        action.onClip.Reverse();
                    }
                } else {
                    var tree = clipFactory.New1D(prefix + " tree", smoothed);
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
