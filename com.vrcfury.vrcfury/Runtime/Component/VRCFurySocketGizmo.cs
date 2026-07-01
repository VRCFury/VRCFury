using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFurySocketGizmo : VRCFuryPlayComponent {
        [Serializable]
        public class GuidedPathStopData {
            public Transform transform;
        }

        [Serializable]
        public class SocketGizmoData {
            public VRCFuryHapticSocket.AddLight type;
            public Vector3 pos;
            public Quaternion rot;
            public bool useRadiusOffset;
            public string name;
            public bool hasHandTouchZone;
            public float handTouchZoneLength;
            public float handTouchZoneRadius;
            public List<GuidedPathStopData> guidedPathStops = new List<GuidedPathStopData>();
        }

        public SocketGizmoData data = new SocketGizmoData();
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
