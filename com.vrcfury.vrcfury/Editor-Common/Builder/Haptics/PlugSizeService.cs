using System.Collections.Generic;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class PlugSizeService {
        private readonly Dictionary<VRCFuryHapticPlug, PlugSizeDetector.SizeResult> sizes = new();

        public PlugSizeDetector.SizeResult GetWorldSize(VRCFuryHapticPlug plug) =>
            sizes.GetOrCreate(plug, () => PlugSizeDetector.GetWorldSize(plug));
    }
}
