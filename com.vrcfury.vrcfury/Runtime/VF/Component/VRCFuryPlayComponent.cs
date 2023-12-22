using System;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    public class VRCFuryPlayComponent : MonoBehaviour {
    }

    public class VRCFurySocketGizmo : VRCFuryPlayComponent {
        public VRCFuryHapticSocket.AddLight type;
        public Vector3 pos;
        public Quaternion rot;
        public bool show = true;

        bool lastShow = false;

        private void Update() {
            if (show && !lastShow) {
                try { EnableSceneLighting?.Invoke(); } catch (Exception) {}
            }
            lastShow = show;
        }

        public static Action EnableSceneLighting;
    }

    public class VRCFuryNoUpdateWhenOffscreen : VRCFuryPlayComponent {
        private void Update() {
            var skin = GetComponent<SkinnedMeshRenderer>();
            if (skin == null) return;
            skin.updateWhenOffscreen = false;
        }
    }
}
