using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using JetBrains.Annotations;
using System.Linq;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsPlugRadiusService {
        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly WorldScaleDetectorService worldScale;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;

        public void Create(VRCFuryHapticPlugEditor.BakeResult bake, string name) {
            var tree = objectTrees.Get(bake.bakeRoot);
            var renderers = bake.renderers.Where(r => r.spsBlendshapes != null).ToArray();
            var left = autoChannelGenerator?.GetBone(bake.bakeRoot, HumanBodyBones.LeftUpperLeg);
            var right = autoChannelGenerator?.GetBone(bake.bakeRoot, HumanBodyBones.RightUpperLeg);
            if (renderers.Length > 0 && left != null && right != null
                && autoChannelGenerator.GetClosestBone(bake.bakeRoot) == HumanBodyBones.Hips) {
                // Hip plugs use half the leg spacing to avoid capturing nearby hands.
                // Follow upper-leg scale, independently of the plug's own scaling.
                var initialRadius = Vector3.Distance(left.worldPosition, right.worldPosition) * 0.5f;
                foreach (var renderer in renderers) {
                    var configureMaterial = renderer.configureMaterial;
                    renderer.configureMaterial = (slot, material) => {
                        var configured = configureMaterial(slot, material);
                        if (configured != null && configured != material) {
                            configured.SetFloatFast(SpsConfigurer.SpsBaseActivationRadiusOverride, initialRadius);
                        }
                        return configured;
                    };
                }
                worldScale.AddScaledProp(
                    left,
                    renderers.Select(renderer => (
                        (UnityEngine.Component)renderer.renderer,
                        "material." + SpsConfigurer.SpsBaseActivationRadiusOverride,
                        initialRadius / left.worldScale.x
                    )).ToArray(),
                    tree
                );
            }
            var proximity = receiverAnimator.GetParameter(bake.socketReceivers, Party.Merged);
            var separation = receiverAnimator.GetAdvancedParam(bake.socketReceivers, Party.Merged, "PointSeparation");
            var inverseScale = worldScale.GetInverseWorldScale(bake.bakeRoot, name);
            var normalizedDistance = controllers.GetFx().CreateUniqueFloat(name + " - Socket position", 1);
            // PointSeparation is world meters; divide by the live plug length. Only scale
            // inversion is cached. Moving the socket passes through one multiplication AAP.
            var product = VFBlendTreeDirect.Create("Socket distance in plug lengths");
            product.Add(separation, normalizedDistance.MakeSetter(1 / bake.localLength));
            var scaled = VFBlendTreeDirect.Create("Normalize socket distance");
            scaled.Add(inverseScale, product);
            tree.Add(normalizedDistance.MakeSetter(0));
            tree.Add(BlendtreeMath.GreaterThan(proximity, 0).And(BlendtreeMath.GreaterThan(separation, 0, true))
                .create(scaled, normalizedDistance.MakeSetter(1)));
            tree.Add(MakeSampler(bake, normalizedDistance));
        }

        private static VFMotion MakeSampler(VRCFuryHapticPlugEditor.BakeResult bake, VFAParam normalizedDistance) {
            var sampler = VFBlendTree1D.Create("Radius at socket", normalizedDistance);
            foreach (var key in bake.radiusCurve.keys) {
                var clip = VFClip.Create("Radius at " + key.time);
                clip.SetScale(bake.mainSender, new Vector3(bake.mainSender.localScale.x,
                    Mathf.Clamp(key.value, bake.localMinimumRadius, bake.localLength),
                    bake.mainSender.localScale.z));
                foreach (var renderer in bake.renderers) {
                    if (renderer.spsBlendshapes == null) continue;
                    clip.SetCurve(renderer.renderer, "material." + SpsConfigurer.SpsRadiusOffsetMultiplier,
                        bake.localRadius > 0 ? Mathf.Clamp(key.value, 0, bake.localLength) / bake.localRadius : 0);
                }
                sampler.Add(key.time, clip);
            }
            return sampler;
        }

    }
}
