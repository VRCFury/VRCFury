using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class Puppet2Axis : NewFeatureModel {
        public string name = "";
        public bool saved;
        public bool setDefaults;
        public float defaultX = 0;
        public float defaultY = 0;
        public bool enableIcon;
        public GuidTexture2d icon;
        
        public State up;
        public State right;
        public State down;
        public State left;
    }
}