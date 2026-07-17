using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class OGBIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new OGBIntegration2();
        }
    }
}