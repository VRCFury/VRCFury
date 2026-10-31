using System.Linq;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsPlugRadiusService {
        [VFAutowired] private readonly DbtLayerService trees;
        [VFAutowired] private readonly OverlappingContactsFixService overlapping;

        internal const float RangeInPlugLengths = 1.6f;
        internal const float MinimumRadius = 0.001f;

        public void Create(VRCFuryHapticPlug plug, VRCFuryHapticPlugEditor.BakeResult bake, string name) {
            var tree = trees.Create(name + " - Sender Radius");
            var proximity = bake.socketReceivers.GetMergedParameter();
            overlapping.Activate();
            tree.Add(MakeSampler(bake, proximity));
        }

        internal static VFMotion MakeSampler(VRCFuryHapticPlugEditor.BakeResult bake, VFAFloat proximity) {
            var sampler = VFBlendTree1D.Create("Radius at socket", proximity);
            // Keep the sender transform invertible even for pointed tips or empty slices.
            // Cap the floor at length so it cannot enlarge the physical contact sphere.
            var minimumRadius = Mathf.Min(MinimumRadius, bake.worldLength);
            // Curve depth and radius are baked meters. Convert depth to proximity thresholds and
            // radius to local sender scale; inherited avatar resizing then scales X/Y/Z together.
            foreach (var key in bake.radiusCurve.keys.Reverse()) {
                var clip = VFClip.Create("Radius at " + key.time);
                // Unity binds scale as a vector even when only one axis has an explicit curve.
                // Preserve the radius/length metadata on X/Z rather than letting it restore 1s.
                clip.SetScale(bake.mainSender, new Vector3(bake.mainSender.localScale.x,
                    Mathf.Clamp(key.value, minimumRadius, bake.worldLength) / bake.mainSender.parent.worldScale.y,
                    bake.mainSender.localScale.z));
                sampler.Add(1 - key.time / (RangeInPlugLengths * bake.worldLength), clip);
            }
            // No contact (proximity 0), or a socket beyond the tip, clamps to the curve's tip radius.
            return sampler;
        }
    }
}
