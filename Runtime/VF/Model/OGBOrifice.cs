using System;
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

        public bool enableDepthAction;
        public State depthAction;
        public float depthActionLength;
        public bool depthActionSelf;
    }
}
