using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * This can build the contacts needed for haptic component depth animations
     */
    [VFService]
    public class HapticAnimContactsService {
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly AvatarManager avatarManager;

        public void CreatePlugAnims(
            ICollection<VRCFuryHapticPlug.PlugDepthAction> actions,
            VFGameObject plugOwner,
            VFGameObject animRoot,
            string name,
            float worldLength
        ) {
            if (actions.Count == 0) return;

            var fx = avatarManager.GetFx();

            var cache = new Dictionary<bool, VFAFloat>();
            VFAFloat GetDistance(bool allowSelf) {
                if (cache.TryGetValue(allowSelf, out var cached)) return cached;
                var prefix = $"{name}/Anim{(allowSelf ? "" : "Others")}";
                var maxDist = actions.Max(a => Math.Max(a.startDistance, a.endDistance));
                var colliderWorldRadius = maxDist * worldLength;
                var contact = CreateFrontBack(prefix, animRoot, colliderWorldRadius, allowSelf, HapticUtils.CONTACT_ORF_MAIN);
                var activeWhen = math.Or(
                    math.GreaterThan(contact.front, contact.back, true),
                    math.GreaterThan(contact.front, 0.8f)
                );
                var distance = math.Map(
                    $"{prefix}/Distance",
                    contact.front,
                    0, 1,
                    maxDist, 0
                );
                var distanceWithoutBehind = math.SetValueWithConditions(
                    $"{prefix}/DistanceWithoutBehind",
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
                var mapped = math.Map(
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

                var clip = actionClipService.LoadState(prefix, depthAction.state, plugOwner);
                if (ClipBuilderService.IsStaticMotion(clip)) {
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
            string name,
            bool worldScale
        ) {
            var fx = avatarManager.GetFx();
            
            var maxDist = Math.Max(0, actions.Max(a => Math.Max(a.startDistance, a.endDistance))) * (worldScale ? 1f : animRoot.transform.lossyScale.z);
            var minDist = Math.Min(0, actions.Min(a => Math.Min(a.startDistance, a.endDistance))) * (worldScale ? 1f : animRoot.transform.lossyScale.z);
            var offset = Math.Max(0, -minDist); // Because the blendtree math can't handle negative values

            var cache = new Dictionary<bool, VFAFloat>();
            VFAFloat GetDistance(bool allowSelf) {
                if (cache.TryGetValue(allowSelf, out var cached)) return cached;

                var prefix = $"{name}/Anim{(allowSelf ? "" : "Others")}";
                var outerRadius = Math.Max(0.01f, maxDist);
                var outer = CreateFrontBack($"{prefix}/Outer", GameObjects.Create("Outer", animRoot), outerRadius, allowSelf, HapticUtils.CONTACT_PEN_MAIN);

                var targets = new List<(MathService.VFAFloatOrConst, MathService.VFAFloatBool)>();
                if (minDist < 0) {
                    var inner = CreateFrontBack($"{prefix}/Inner", GameObjects.Create("Inner", animRoot), -minDist, allowSelf, HapticUtils.CONTACT_PEN_MAIN, Vector3.forward * minDist);
                    // Some of the animations have an inside depth (negative distance)
                    targets.Add((
                        math.Map($"{prefix}/Inner/Distance", inner.front, 1, 0, offset+minDist, offset+0),
                        math.GreaterThan(outer.front, 1, true)
                    ));
                }
                if (maxDist > 0) {
                    // Some of the animations have an outside depth (positive distance)
                    targets.Add((
                        math.Map($"{prefix}/Outer/Distance", outer.front, 1, 0, offset+0, offset+outerRadius),
                        math.GreaterThan(outer.front, 0)
                    ));
                }
                // If plug isn't in either region, set to 0
                targets.Add((fx.NewFloat($"{prefix}/MaxDist", def: offset+outerRadius), null));
                var unsmoothed = math.SetValueWithConditions(
                    $"{prefix}/Distance",
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
                var mapped = math.Map(
                    $"{prefix}/Mapped",
                    unsmoothed,
                    offset + depthAction.startDistance * (worldScale ? 1f : animRoot.transform.lossyScale.z),
                    offset + depthAction.endDistance * (worldScale ? 1f : animRoot.transform.lossyScale.z),
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

                var clip = actionClipService.LoadState(prefix, depthAction.state, socketOwner);
                if (ClipBuilderService.IsStaticMotion(clip)) {
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
            var fx = avatarManager.GetFx();
            var frontParam = fx.NewFloat($"{paramName}/FrontOthers");
            HapticUtils.AddReceiver(parent, posOffset, frontParam.Name(),
                "FrontOthers", radius, new[] { contactTag },
                HapticUtils.ReceiverParty.Others);
            var backParam = fx.NewFloat($"{paramName}/BackOthers");
            HapticUtils.AddReceiver(parent, posOffset + Vector3.forward * -0.01f, backParam.Name(),
                "BackOthers", radius, new[] { contactTag },
                HapticUtils.ReceiverParty.Others);
            
            if (allowSelf) {
                var frontSelfParam = fx.NewFloat($"{paramName}/FrontSelf");
                HapticUtils.AddReceiver(parent, posOffset, frontSelfParam.Name(),
                    "FrontSelf", radius, new[] { contactTag },
                    HapticUtils.ReceiverParty.Self);
                frontParam = math.Max(frontParam, frontSelfParam);
                
                var backSelfParam = fx.NewFloat($"{paramName}/BackSelf");
                HapticUtils.AddReceiver(parent, posOffset + Vector3.forward * -0.01f, backSelfParam.Name(),
                    "BackSelf", radius, new[] { contactTag },
                    HapticUtils.ReceiverParty.Self);
                backParam = math.Max(backParam, backSelfParam);
            }
            
            return new FrontBack {
                front = frontParam,
                back = backParam
            };
        }
    }
}
