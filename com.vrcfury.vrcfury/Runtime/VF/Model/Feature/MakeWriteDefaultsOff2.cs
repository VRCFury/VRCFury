using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class MakeWriteDefaultsOff2 : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new FixWriteDefaults {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            };
        }
    }
}