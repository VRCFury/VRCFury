using com.vrcfury.api.Components;
using UnityEngine;

namespace com.vrcfury.api {
    public static class FuryComponents {
        public static FuryArmatureLink CreateArmatureLink(GameObject obj) {
            return new FuryArmatureLink(obj);
        }
    }
}
