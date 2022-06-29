using System;
using System.Collections.Generic;
using UnityEngine;
using VRCF.Model.Feature;

namespace VRCF.Model {

[Serializable]
public class VRCFuryConfig {
    public int version;
    [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();
}

}
