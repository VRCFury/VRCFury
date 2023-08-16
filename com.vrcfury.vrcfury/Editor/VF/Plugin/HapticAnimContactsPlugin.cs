using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;

namespace VF.Plugin {
    /**
     * This can build the contacts needed for haptic component depth animations
     */
    public class HapticAnimContactsPlugin : FeaturePlugin {
        public void CreatePlugAnims(
            ICollection<VRCFuryHapticPlug.PlugDepthAction> actions,
            VFGameObject plugOwner,
            VFGameObject animRoot,
            string name,
            float worldLength
        ) {
            if (actions.Count == 0) return;

            var fx = GetFx();
            var smoothing = GetPlugin<ParamSmoothingPlugin>();

            var cache = new Dictionary<bool, VFAFloat>();
            VFAFloat GetDistance(bool allowSelf) {
                if (cache.TryGetValue(allowSelf, out var cached)) return cached;
                var prefix = $"{name}/Anim{(allowSelf ? "" : "Others")}";
                var maxDist = actions.Max(a => Math.Max(a.startDistance, a.endDistance));
                var colliderWorldRadius = maxDist * worldLength;
                var contact = CreateFrontBack(prefix, animRoot, colliderWorldRadius, allowSelf, HapticUtils.CONTACT_ORF_MAIN);
                var activeWhen = smoothing.GreaterThan(contact.front, contact.back, true)
                    .Or(contact.front.IsGreaterThan(0.8f));
                var distance = smoothing.Map(
                    $"{prefix}/Distance",
                    contact.front,
                    0, 1,
                    maxDist, 0
                );
                var distanceWithoutBehind = smoothing.SetValueWithConditions(
                    $"{prefix}/DistanceWithoutBehind",
                    0, maxDist,
                    distance.GetDefault(),
                    (distance, activeWhen),
                    (fx.NewFloat($"{prefix}/MaxDist", def: distance.GetDefault()), null)
                );
                cache[allowSelf] = distanceWithoutBehind;
                return distanceWithoutBehind;
            }

            var actionNum = 0;
            foreach (var depthAction in actions) {
                actionNum++;
                var prefix = $"{name}/Anim{actionNum}";

                var distance = GetDistance(depthAction.enableSelf);
                var mapped = smoothing.Map(
                    $"{prefix}/Mapped",
                    distance,
                    depthAction.startDistance, depthAction.endDistance,
                    0, 1
                );
                var smoothParam = smoothing.Smooth(
                    $"{prefix}/Smoothed",
                    mapped,
                    depthAction.smoothingSeconds
                );

                var layer = fx.NewLayer("Depth Animation " + actionNum + " for " + name);
                var off = layer.NewState("Off");
                var on = layer.NewState("On");

                var clip = LoadState(prefix, depthAction.state, plugOwner);
                if (ClipBuilder.IsStaticMotion(clip)) {
                    var tree = fx.NewBlendTree(prefix + " tree");
                    tree.blendType = BlendTreeType.Simple1D;
                    tree.useAutomaticThresholds = false;
                    tree.blendParameter = smoothParam.Name();
                    tree.AddChild(fx.GetEmptyClip(), 0);
                    tree.AddChild(clip, 1);
                    on.WithAnimation(tree);
                } else {
                    on.WithAnimation(clip).MotionTime(smoothParam);
                }

                var onWhen = smoothParam.IsGreaterThan(0.01f);
                off.TransitionsTo(on).When(onWhen);
                on.TransitionsTo(off).When(onWhen.Not());
            }
        }

        public void CreateSocketAnims(
            ICollection<VRCFuryHapticSocket.DepthAction> actions,
            VFGameObject socketOwner,
            VFGameObject animRoot,
            string name
        ) {
            var fx = GetFx();
            var smoothing = GetPlugin<ParamSmoothingPlugin>();

            var cache = new Dictionary<bool, VFAFloat>();
            VFAFloat GetDistance(bool allowSelf) {
                if (cache.TryGetValue(allowSelf, out var cached)) return cached;

                var prefix = $"{name}/Anim{(allowSelf ? "" : "Others")}";
                var maxDist = Math.Max(0, actions.Max(a => Math.Max(a.startDistance, a.endDistance)));
                var minDist = Math.Min(0, actions.Min(a => Math.Min(a.startDistance, a.endDistance)));
                var outerRadius = Math.Max(0.01f, maxDist);
                var outer = CreateFrontBack($"{prefix}/Outer", animRoot, outerRadius, allowSelf, HapticUtils.CONTACT_PEN_MAIN);

                var targets = new List<(VFAFloat, VFACondition)>();
                if (minDist < 0) {
                    var inner = CreateFrontBack($"{prefix}/Inner", animRoot, -minDist, allowSelf, HapticUtils.CONTACT_PEN_MAIN, Vector3.forward * minDist);
                    // Some of the animations have an inside depth (negative distance)
                    var test = outer.front.IsGreaterThanOrEquals(1);
                    targets.Add((
                        smoothing.Map($"{prefix}/Inner/Distance", inner.front, 1, 0, minDist, 0),
                        outer.front.IsGreaterThanOrEquals(1)
                            .And(smoothing.GreaterThan(inner.front, inner.back, true))
                    ));
                }
                if (maxDist > 0) {
                    // Some of the animations have an outside depth (positive distance)
                    targets.Add((
                        smoothing.Map($"{prefix}/Outer/Distance", outer.front, 1, 0, 0, outerRadius),
                        outer.front.IsGreaterThan(0).And(smoothing.GreaterThan(outer.front, outer.back, true))
                    ));
                }
                // If plug isn't in either region, set to 0
                targets.Add((fx.NewFloat($"{prefix}/MaxDist", def: outerRadius), null));
                var unsmoothed = smoothing.SetValueWithConditions(
                    $"{prefix}/Distance",
                    minDist, outerRadius,
                    outerRadius,
                    targets.ToArray()
                );

                var result = unsmoothed;
                cache[allowSelf] = result;
                return result;
            }

            var actionNum = 0;
            foreach (var depthAction in actions) {
                actionNum++;
                var prefix = $"{name}/Anim{actionNum}";

                var unsmoothed = GetDistance(depthAction.enableSelf);
                var mapped = smoothing.Map(
                    $"{prefix}/Mapped",
                    unsmoothed,
                    depthAction.startDistance, depthAction.endDistance,
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

                var clip = LoadState(prefix, depthAction.state, socketOwner);
                if (ClipBuilder.IsStaticMotion(clip)) {
                    var tree = fx.NewBlendTree(prefix + " tree");
                    tree.blendType = BlendTreeType.Simple1D;
                    tree.useAutomaticThresholds = false;
                    tree.blendParameter = smoothed.Name();
                    tree.AddChild(fx.GetEmptyClip(), 0);
                    tree.AddChild(clip, 1);
                    on.WithAnimation(tree);
                } else {
                    on.WithAnimation(clip).MotionTime(smoothed);
                }

                var onWhen = smoothed.IsGreaterThan(0.01f);
                off.TransitionsTo(on).When(onWhen);
                on.TransitionsTo(off).When(onWhen.Not());
            }
        }

        private class FrontBack {
            public VFAFloat front;
            public VFAFloat back;
        }
        private FrontBack CreateFrontBack(string paramName, VFGameObject parent, float radius, bool allowSelf, string contactTag, Vector3? _posOffset = null) {
            var posOffset = _posOffset.GetValueOrDefault(Vector3.zero);
            var fx = GetFx();
            var frontParam = fx.NewFloat($"{paramName}/Front");
            HapticUtils.AddReceiver(parent, posOffset, frontParam.Name(),
                "Front", radius, new[] { contactTag },
                allowSelf: allowSelf);
            var backParam = fx.NewFloat($"{paramName}/Back");
            HapticUtils.AddReceiver(parent, posOffset + Vector3.forward * -0.01f, backParam.Name(),
                "Back", radius, new[] { contactTag },
                allowSelf: allowSelf);
            return new FrontBack {
                front = frontParam,
                back = backParam
            };
        }
    }
}
