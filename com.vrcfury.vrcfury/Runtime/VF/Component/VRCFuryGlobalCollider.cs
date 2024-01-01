using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("VRCFury/VRCFury - Global Collider")]
    public class VRCFuryGlobalCollider : VRCFuryComponent {
        public float radius = 0.1f;
        public float height = 0;
        public Transform rootTransform;

        public Transform GetTransform() {
            return rootTransform != null ? rootTransform : transform;
        }
    }
}
