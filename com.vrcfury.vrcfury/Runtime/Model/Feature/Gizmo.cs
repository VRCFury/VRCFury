using System;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class Gizmo : NewFeatureModel {
        public Vector3 rotation;
        public string text;
        public float sphereRadius;
        public float arrowLength;
    }
}