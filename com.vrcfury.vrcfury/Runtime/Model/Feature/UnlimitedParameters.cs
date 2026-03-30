using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class UnlimitedParameters : NewFeatureModel {
        public bool includeBools = false;
        public bool includePuppets = false;
    }
}
