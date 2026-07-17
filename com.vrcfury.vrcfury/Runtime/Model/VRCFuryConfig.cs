using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model.Feature;

namespace VF.Model {
    [Serializable]
    internal class VRCFuryConfig {
        [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();
    }
}