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
}
