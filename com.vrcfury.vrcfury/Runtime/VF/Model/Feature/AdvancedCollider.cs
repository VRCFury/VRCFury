using System;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class AdvancedCollider : NewFeatureModel {
        public float radius = 0.1f;
        public float height = 0;
        public Transform rootTransform;
        public string colliderName;
    }
}
