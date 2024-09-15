using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class ReorderMenuItem : NewFeatureModel {
        public string path;
        public int position;
    }
}