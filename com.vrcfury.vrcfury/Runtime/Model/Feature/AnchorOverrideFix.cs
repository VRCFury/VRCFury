using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class AnchorOverrideFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AnchorOverrideFix2();
        }
    }
}