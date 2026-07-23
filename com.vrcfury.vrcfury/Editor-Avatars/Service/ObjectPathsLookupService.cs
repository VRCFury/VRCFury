using System.Collections.Generic;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class ObjectPathsLookupService {
        private readonly List<VRCFObjectPathCache> lookups = new List<VRCFObjectPathCache>();

        public IReadOnlyList<VRCFObjectPathCache> GetLookups() {
            return lookups;
        }

        public VRCFObjectPathCache Capture(VFGameObject avatarObject) {
            var paths = new VRCFObjectPathCache(avatarObject);
            lookups.Insert(0, paths);
            return paths;
        }
    }
}
