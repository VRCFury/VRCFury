using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class LegacyPrefabSupport : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new LegacyPrefabSupport2();
        }
    }
}