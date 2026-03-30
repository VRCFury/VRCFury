using System;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFurySocketGizmo : VRCFuryPlayComponent {
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
}