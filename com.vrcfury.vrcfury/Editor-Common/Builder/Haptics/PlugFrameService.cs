using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class PlugFrameService {
        [VFAutowired] private readonly PlugSizeService sizes;
        private readonly Dictionary<VRCFuryHapticPlug, VFGameObject> roots = new();

        public VFGameObject Get(VRCFuryHapticPlug plug) => roots.GetOrCreate(plug, () => {
            var size = sizes.GetWorldSize(plug);
            var root = GameObjects.Create("BakedSpsPlug", plug.owner());
            root.localPosition = size.localPosition;
            root.localRotation = size.localRotation;
            // The shader uses this frame's scale as the resize factor since baking.
            root.worldScale = Vector3.one;
            return root;
        });
    }
}
