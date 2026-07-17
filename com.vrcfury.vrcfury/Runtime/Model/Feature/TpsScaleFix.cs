using System;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class TpsScaleFix : NewFeatureModel {
        [NonSerialized] public Renderer singleRenderer;
    }
}