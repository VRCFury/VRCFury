using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("VRCFury/SPS Touch Zone (VRCFury)")]
    internal class VRCFuryHapticTouchReceiver : VRCFuryComponent {
        public new string name;
        public float radius = 0.1f;
        [Min(0)] public float height;
    }
}
