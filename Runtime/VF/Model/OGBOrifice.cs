using System;
using UnityEngine;

namespace VF.Model {
    public class OGBOrifice : VRCFuryComponent {
        public enum AddLight {
            None,
            Hole,
            Ring
        }

        public enum EnableTouchZone {
            Auto,
            On,
            Off
        }

        public AddLight addLight;
        public new string name;
        public EnableTouchZone enableHandTouchZone2 = EnableTouchZone.Auto;
        public float length;
        public bool addMenuItem = false;
    }
}
