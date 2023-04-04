using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VF.Model;

namespace VF.Component {
    public class VRCFuryHapticSocket : VRCFuryComponent {
        public enum AddLight {
            None,
            Hole,
            Ring,
            Auto
        }

        public enum EnableTouchZone {
            Auto,
            On,
            Off
        }

        public AddLight addLight = AddLight.None;
        public new string name;
        public EnableTouchZone enableHandTouchZone2 = EnableTouchZone.Auto;
        public float length;
        public bool addMenuItem = false;
        public bool enableAuto = true;
        public Vector3 position;
        public Vector3 rotation;

        public List<DepthAction> depthActions = new List<DepthAction>();

        [Serializable]
        public class DepthAction {
            public State state;
            public float minDepth;
            public float maxDepth;
            public bool enableSelf;
            public bool ResetMePlease;
        }
    }
}
