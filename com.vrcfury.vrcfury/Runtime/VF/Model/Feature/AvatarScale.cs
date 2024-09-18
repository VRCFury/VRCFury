using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class AvatarScale : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AvatarScale2();
        }
    }
}