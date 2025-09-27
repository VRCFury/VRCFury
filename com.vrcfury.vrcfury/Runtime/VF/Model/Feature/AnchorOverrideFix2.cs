using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class AnchorOverrideFix2 : NewFeatureModel {
        public bool ignoreExisting = false;
        public bool ignoreWorldDrops = true;
    }
}
