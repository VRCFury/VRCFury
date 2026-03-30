using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFuryNoUpdateWhenOffscreen : VRCFuryPlayComponent {
        private void Update() {
            var skin = GetComponent<SkinnedMeshRenderer>();
            if (skin == null) return;
            skin.updateWhenOffscreen = false;
        }
    }
}