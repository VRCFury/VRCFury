using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class BoundingBoxFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new BoundingBoxFix2();
        }
    }
}