using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class MakeWriteDefaultsOff : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new FixWriteDefaults {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            };
        }
    }
}