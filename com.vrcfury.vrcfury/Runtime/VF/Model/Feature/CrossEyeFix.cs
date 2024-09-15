using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class CrossEyeFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new CrossEyeFix2();
        }
    }
}