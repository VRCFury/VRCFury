using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class SpsOptions : NewFeatureModel {
        public GuidTexture2d menuIcon;
        public string menuPath;
        public bool saveSockets = false;
    }
}