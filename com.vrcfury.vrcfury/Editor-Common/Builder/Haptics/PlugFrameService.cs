using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class PlugFrameService {
        [VFAutowired] private readonly PlugSizeService sizes;
        private readonly Dictionary<VRCFuryHapticPlug, Frames> frames = new();

        public class Frames {
            public VFGameObject bakeRoot;
            public VFGameObject oneSpace;
            public VFGameObject worldSpace;
        }

        public Frames Get(VRCFuryHapticPlug plug) => frames.GetOrCreate(plug, () => {
            var size = sizes.GetWorldSize(plug);
            var root = GameObjects.Create("BakedSpsPlug", plug.owner());
            root.localPosition = size.localPosition;
            root.localRotation = size.localRotation;
            var oneSpace = GameObjects.Create("OneSpace", root);
            oneSpace.worldScale = Vector3.one;
            var worldSpace = GameObjects.Create("WorldSpace", root);
            ConstraintUtils.MakeWorldSpace(worldSpace);
            return new Frames { bakeRoot = root, oneSpace = oneSpace, worldSpace = worldSpace };
        });
    }
}
