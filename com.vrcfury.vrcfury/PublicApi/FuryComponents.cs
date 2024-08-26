using com.vrcfury.api.Components;
using JetBrains.Annotations;
using UnityEngine;

namespace com.vrcfury.api {
    /** Public API for creating VRCFury components */
    [PublicAPI]
    public static class FuryComponents {

        public static FuryArmatureLink CreateArmatureLink(GameObject obj) {
            return new FuryArmatureLink(obj);
        }

        public static FuryFullController CreateFullController(GameObject obj) {
            return new FuryFullController(obj);
        }

        public static FuryToggle CreateToggle(GameObject obj) {
            return new FuryToggle(obj);
        }
    }
}
