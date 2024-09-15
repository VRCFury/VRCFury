using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class DirectTreeOptimizer : NewFeatureModel {
        [NonSerialized] public bool managedOnly = false;
    }
}