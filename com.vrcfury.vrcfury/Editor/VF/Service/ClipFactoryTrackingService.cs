using System.Collections.Generic;
using UnityEngine;
using VF.Injector;

namespace VF.Service {
    [VFService]
    internal class ClipFactoryTrackingService {
        private readonly HashSet<Motion> created = new HashSet<Motion>();
        
        public void MarkCreated(Motion motion) {
            created.Add(motion);
        }

        public bool Created(Motion motion) {
            return created.Contains(motion);
        }
    }
}
