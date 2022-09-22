using System;
using UnityEngine;

namespace VF.Model {
    public enum AddLight {
        None,
        Hole,
        Ring
    }
    
    public class OGBOrifice : MonoBehaviour {
        public AddLight addLight;
        public String name;
        public float length;
    }
}
