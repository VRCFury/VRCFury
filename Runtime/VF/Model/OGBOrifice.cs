using System;
using UnityEngine;

namespace VF.Model {
    public enum AddLight {
        None,
        Hole,
        Ring
    }
    
    public class OGBOrifice : VRCFuryComponent {
        public AddLight addLight;
        public new string name;
        public bool enableHandTouchZone = true;
        public float length;
        public bool addMenuItem = false;
    }
}
