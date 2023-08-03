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
    }

    public class VRCFuryNoUpdateWhenOffscreen : VRCFuryPlayComponent {
        private void Update() {
            var skin = GetComponent<SkinnedMeshRenderer>();
            if (skin == null) return;
            skin.updateWhenOffscreen = false;
        }
    }
}
