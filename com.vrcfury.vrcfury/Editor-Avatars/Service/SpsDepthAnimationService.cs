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
        [VFAutowired] private readonly IsObjectEnabledService objectEnabled;

        public void CreateAnims(
            IEnumerable<VRCFuryHapticSocket.DepthActionNew> actions,
            VFGameObject spsComponentOwner,
            string name,
            Func<Party, Func<string, VFAParam>> getMeasurements,
            VFGameObject bakeRoot,
            string measurement = SpsParamDepth,
            VFAParam weight = null
        ) {
            var calculations = objectTrees.Get(bakeRoot);
            var increasing = measurement == SpsParamPlugEntryRadius;
            var controls = new Dictionary<(string input, float off, float on, float seconds, bool reverse, string filters), VFAParam>();
            var actionNum = 0;
            foreach (var depthAction in actions) {
                if (!depthAction.enableSelf && !depthAction.enableOthers) continue;
                var plugDepth = spsComponentOwner.GetComponent<VRCFuryHapticPlug>() != null;
                if (plugDepth && !depthAction.allowHole && !depthAction.allowRing) continue;
                actionNum++;
                var prefix = $"{name}/Anim/{actionNum}";

                var measurements = getMeasurements(!depthAction.enableOthers ? Party.Self : depthAction.enableSelf ? Party.Merged : Party.Others);
                var filters = new List<(VFAParam input, bool expected)>();
                if (plugDepth) {
                    if (!depthAction.allowHole || !depthAction.allowRing) filters.Add((measurements(SpsSocketIsHole), depthAction.allowHole));
                }
                var allowed = BlendtreeMath.True();
                var allowedInState = fx.Always();
                var filterKey = "";
                foreach (var (input, expected) in filters) {
                    var condition = BlendtreeMath.GreaterThan(input, 0.5f);
                    var stateCondition = input.IsGreaterThan(0.5f);
                    allowed = allowed.And(expected ? condition : condition.Not());
                    allowedInState = allowedInState.And(expected ? stateCondition : stateCondition.Not());
                    filterKey += input.name + "=" + expected + ";";
                }
                var units = depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? SpsUnitsPlugLengths
                    : depthAction.units == VRCFuryHapticSocket.DepthActionUnits.Meters ? SpsUnitsMeters : SpsUnitsLocal;
                var unsmoothed = measurements(GetSpsMagicParam(measurement, units));
                var action = actionClipService.LoadStateAdv(prefix, depthAction.actionSet, spsComponentOwner, ActionClipService.MotionTimeMode.Auto);
                var reverseClip = action.useMotionTime && !increasing && depthAction.reverseClip;
                var useRawInput = depthAction.smoothingSeconds <= 0 && !action.useMotionTime
                    && depthAction.range.Min() < depthAction.range.Max();
                var parameter = unsmoothed;
                var offThreshold = increasing ? depthAction.range.Min() : depthAction.range.Max();
                var onThreshold = increasing ? depthAction.range.Max() : depthAction.range.Min();
                if (!useRawInput) {
                    var seconds = Math.Max(0, Math.Min(10, depthAction.smoothingSeconds));
                    var far = reverseClip ? 1f : 0f;
                    parameter = controls.GetOrCreate((unsmoothed.name, offThreshold, onThreshold, seconds, reverseClip, filterKey), () =>
                        smoothing.Smooth(
                            calculations,
                            prefix + (seconds > 0 ? "/Smoothed" : "/Mapped"),
                            seconds,
                            far,
                            output => allowed.create(
                                BlendtreeMath.Map(output, unsmoothed, offThreshold, onThreshold, far, 1 - far),
                                output.MakeSetter(far)
                            ),
                            minSupported: 0,
                            maxSupported: 1
                        )
                    );
                    offThreshold = 0;
                    onThreshold = 1;
                }

                var layer = fx.NewLayer($"{name} - {(increasing ? "Plug Entry Radius" : "Depth")} Animations - {actionNum} - Action");
                var off = reverseClip ? null : layer.NewState("Off");
                var on = layer.NewState("On");
                if (!action.useMotionTime) {
                    var tree = VFBlendTree1D.Create(prefix + " tree", parameter);
                    if (useRawInput && !increasing) {
                        tree.Add(onThreshold, action.onClip);
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                    } else {
                        tree.Add(offThreshold, clipFactory.GetEmptyClip());
                        tree.Add(onThreshold, action.onClip);
                    }
                    // Keep inactive actions in an empty state so lower layers can drive shared properties.
                    // LayerToTree may still merge this layer when its bindings are independent.
                    on.WithAnimation(tree);
                } else {
                    on.WithAnimation(action.onClip).MotionTime(parameter);
                }
                if (reverseClip) continue;

                VFAParam weightedRadius = null;
                if (!plugDepth && !increasing && depthAction.plugRadiusForFullWeight > 0) {
                    // The measurement is radius in socket-local units. A 1D tree clamps
                    // at both endpoints and weights the entire motion, including motion-time clips.
                    weightedRadius = measurements(SpsPlugRadiusLocal);
                    on.WithAnimation(VFBlendTree1D.CreateWithData(prefix + "/Plug width weight",
                        weightedRadius,
                        (depthAction.plugRadiusForZeroWeight, clipFactory.GetEmptyClip()),
                        (depthAction.plugRadiusForFullWeight, on.motion)));
                }
                if (weight != null) {
                    var weighted = VFBlendTreeDirect.Create(prefix + "/SPS weight");
                    weighted.Add(weight, on.motion);
                    on.WithAnimation(weighted);
                }

                // Ignore residuals from smoothing. Adjacent floats at this cutoff are
                // far enough apart that the animator can interpolate without subnormals.
                const float smoothedOffThreshold = 1e-30f;
                var onWhen = useRawInput && !increasing
                    ? parameter.IsLessThan(offThreshold)
                    : parameter.IsGreaterThan(useRawInput ? offThreshold : smoothedOffThreshold);
                if (useRawInput && filters.Count > 0) onWhen = onWhen.And(allowedInState);
                if (weightedRadius != null) onWhen = onWhen.And(weightedRadius.IsGreaterThan(depthAction.plugRadiusForZeroWeight));
                onWhen = onWhen.And(objectEnabled.Get(bakeRoot).IsGreaterThan(0));
                off.TransitionsTo(on).When(onWhen);
                on.TransitionsTo(off).When(onWhen.Not());
            }
        }
    }
}
