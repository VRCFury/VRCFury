using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class FixWriteDefaults : NewFeatureModel {
        public enum FixWriteDefaultsMode {
            Auto,
            ForceOff,
            ForceOn,
            Disabled
        }
        public FixWriteDefaultsMode mode = FixWriteDefaultsMode.Auto;
    }
}