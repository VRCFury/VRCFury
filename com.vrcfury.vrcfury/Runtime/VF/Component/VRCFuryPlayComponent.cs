using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    public class VRCFuryPlayComponent : MonoBehaviour {
    }

    public class VRCFurySocketGizmo : VRCFuryPlayComponent {
        public VRCFuryHapticSocket.AddLight type;
    }
}
