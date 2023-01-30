using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model {
    public class OGBOrifice : VRCFuryComponent {
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

        public List<DepthAction> depthActions = new List<DepthAction>();

        [Serializable]
        public class DepthAction {
            public State state;
            public float maxDepth;
            public bool enableSelf;
            public bool ResetMePlease;
        }
    }
}
