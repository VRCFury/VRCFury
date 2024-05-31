using com.vrcfury.api.Components;
using JetBrains.Annotations;
using UnityEngine;

namespace com.vrcfury.api {
    /** Public API for creating VRCFury components */
    public static class FuryComponents {
        [UsedImplicitly]
        public static FuryArmatureLink CreateArmatureLink(GameObject obj) {
            return new FuryArmatureLink(obj);
        }
        
        [UsedImplicitly]
        public static FuryFullController CreateFullController(GameObject obj) {
            return new FuryFullController(obj);
        }

        [UsedImplicitly]
        public static FuryToggle CreateToggle(GameObject obj) {
            return new FuryToggle(obj);
        }
    }
}
