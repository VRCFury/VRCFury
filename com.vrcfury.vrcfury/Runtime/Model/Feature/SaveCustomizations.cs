using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class SaveCustomizations : NewFeatureModel {
        public string source;
        public string menuPath;
        public GuidTexture2d icon;
        public bool bakeIntoDefaults;
    }
}
