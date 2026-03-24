using System;
using UnityEditor;
using UnityEngine;
using VF.Component;

namespace VF.Model {
    [AddComponentMenu("")]
    internal class VRCFuryDebugInfo : VRCFuryComponent {
        public string title;
        public string debugInfo;
        public bool warn;
    }
}
