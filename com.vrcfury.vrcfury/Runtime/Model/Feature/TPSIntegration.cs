using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class TPSIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new TPSIntegration2();
        }
    }
}