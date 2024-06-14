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
using VF.Utils;
using VF.Utils.Controller;

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
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public void CreatePlugAnims(
            ICollection<VRCFuryHapticPlug.PlugDepthAction> actions,
            VFGameObject plugOwner,
            VFGameObject animRoot,
            string name,
            float worldLength,
            bool useHipAvoidance
        ) {
            if (actions.Count == 0) return;

            var fx = avatarManager.GetFx();

            var cache = new Dictionary<bool, VFAFloat>();
            VFAFloat GetDistance(bool allowSelf) {
                if (cache.TryGetValue(allowSelf, out var cached)) return cached;
                var prefix = $"{name}/Anim{(allowSelf ? "" : "Others")}";
                var maxDist = actions.Max(a => Math.Max(a.startDistance, a.endDistance));
                var colliderWorldRadius = maxDist * worldLength;
                var contact = CreateFrontBack(prefix, animRoot, colliderWorldRadius, allowSelf, HapticUtils.TagTpsOrfRoot, useHipAvoidance);
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
                if (clip.IsStatic()) {
                    var tree = clipFactory.NewBlendTree(prefix + " tree");
                    tree.blendType = BlendTreeType.Simple1D;
                    tree.useAutomaticThresholds = false;
                    tree.blendParameter = smoothParam;
                    tree.AddChild(clipFactory.GetEmptyClip(), 0);
                    tree.AddChild(clip, 1);
                    on.WithAnimation(tree);
                } else {
                    clip.SetLooping(false);
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
            string name,
            SocketContacts contacts,
            bool worldScale
        ) {
            var fx = avatarManager.GetFx();

            var actionNum = 0;
            foreach (var depthAction in actions) {
                actionNum++;
                var prefix = $"{name}/Anim/{actionNum}";

                var unsmoothed = depthAction.rangeInPlugLengths
                    ? ( depthAction.enableSelf ? contacts.closestDistancePlugLengths.Value : contacts.others.plugDistancePlugLengths.Value )
                    : ( depthAction.enableSelf ? contacts.closestDistanceMeters.Value : contacts.others.plugDistanceMeters.Value );
                var mapped = math.Map(
                    $"{prefix}/Mapped",
                    unsmoothed,
                    depthAction.range.Max() * (worldScale ? 1f : socketOwner.worldScale.z),
                    depthAction.range.Min() * (worldScale ? 1f : socketOwner.worldScale.z),
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
                if (clip.IsStatic()) {
                    var tree = clipFactory.NewBlendTree(prefix + " tree");
                    tree.blendType = BlendTreeType.Simple1D;
                    tree.useAutomaticThresholds = false;
                    tree.blendParameter = smoothed;
                    tree.AddChild(clipFactory.GetEmptyClip(), 0);
                    tree.AddChild(clip, 1);
                    on.WithAnimation(tree);
                } else {
                    clip.SetLooping(false);
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
        private FrontBack CreateFrontBack(string paramName, VFGameObject parent, float radius, bool self, string contactTag, bool useHipAvoidance, Vector3? _posOffset = null) {
            var posOffset = _posOffset.GetValueOrDefault(Vector3.zero);
            var front = hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                obj = parent,
                pos = posOffset,
                paramName = $"{paramName}/Front",
                objName = "Front",
                radius = radius,
                tags = new[] { contactTag },
                party = self ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others,
                useHipAvoidance = useHipAvoidance
            });
            var back = hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                obj = parent,
                pos = posOffset + Vector3.forward * -0.01f,
                paramName = $"{paramName}/Back",
                objName = "Back",
                radius = radius,
                tags = new[] { contactTag },
                party = self ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others,
                useHipAvoidance = useHipAvoidance
            });

            return new FrontBack {
                front = front,
                back = back
            };
        }
    }
}
