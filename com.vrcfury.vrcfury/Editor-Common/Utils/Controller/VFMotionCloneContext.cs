using System.Collections.Generic;

namespace VF.Utils.Controller {
    internal sealed class VFMotionCloneContext {
        private readonly Dictionary<VFMotion, VFMotion> clonesBySource = new Dictionary<VFMotion, VFMotion>();

        public bool TryGet(VFMotion source, out VFMotion clone) {
            return clonesBySource.TryGetValue(source, out clone);
        }

        public void Add(VFMotion source, VFMotion clone) {
            if (source == null || clone == null) return;
            clonesBySource[source] = clone;
        }
    }
}
