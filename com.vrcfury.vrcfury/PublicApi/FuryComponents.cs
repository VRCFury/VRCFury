using com.vrcfury.api.Components;
using UnityEngine;

namespace com.vrcfury.api {
    /** Public API for creating VRCFury components */
    public static class FuryComponents {
        public static FuryArmatureLink CreateArmatureLink(GameObject obj) {
            return new FuryArmatureLink(obj);
        }
        
        public static FuryFullController CreateFullController(GameObject obj) {
            return new FuryFullController(obj);
        }
    }
}
