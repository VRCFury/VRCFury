using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class PlugFrameService {
        [VFAutowired] private readonly PlugSizeService sizes;
        [VFAutowired] [CanBeNull] private readonly SpsAutoChannelGenerator autoChannelGenerator;
        private readonly Dictionary<VRCFuryHapticPlug, VFGameObject> roots = new();

        public VFGameObject Get(VRCFuryHapticPlug plug) => roots.GetOrCreate(plug, () => {
            var size = sizes.GetWorldSize(plug);
            var root = GameObjects.Create("BakedSpsPlug", plug.owner());
            root.localPosition = size.localPosition;
            root.localRotation = size.localRotation;
            if (autoChannelGenerator?.GetClosestBone(plug.owner()) == HumanBodyBones.Hips) {
                // Correct imported roll before mesh baking; keep the authored plug pose intact.
                var forward = root.TransformDirection(Vector3.forward);
                var up = Vector3.ProjectOnPlane(Vector3.up, forward);
                if (up.sqrMagnitude > 0.000001f) root.worldRotation = Quaternion.LookRotation(forward, up);
            }
            // The shader uses this frame's scale as the resize factor since baking.
            root.worldScale = Vector3.one;
            return root;
        });
    }
}
