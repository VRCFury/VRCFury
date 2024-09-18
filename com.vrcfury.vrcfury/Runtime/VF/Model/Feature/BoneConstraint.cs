using System;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class BoneConstraint : NewFeatureModel {
        public GameObject obj;
        public HumanBodyBones bone;
    }
}