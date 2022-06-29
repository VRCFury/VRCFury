using System;
using UnityEngine;
using System.Collections.Generic;
using VRCF.Model.Feature;
using UnityEditor;

namespace VRCF.Model {

[Serializable]
public class VRCFuryConfig {
    public int version;
    [SerializeReference] public List<VRCF.Model.Feature.FeatureModel> features = new List<VRCF.Model.Feature.FeatureModel>();
}

}
