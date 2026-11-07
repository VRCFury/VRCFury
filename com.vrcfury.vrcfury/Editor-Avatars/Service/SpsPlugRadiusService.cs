using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
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

        public void Create(VRCFuryHapticPlugEditor.BakeResult bake, string name) {
            var tree = objectTrees.Get(bake.bakeRoot);
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
