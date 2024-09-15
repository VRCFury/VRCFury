using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class Toes : NewFeatureModel {
        public State down;
        public State up;
        public State splay;
    }
}