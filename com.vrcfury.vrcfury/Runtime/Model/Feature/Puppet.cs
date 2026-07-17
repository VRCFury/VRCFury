using System;
using System.Collections.Generic;

namespace VF.Model.Feature {
    [Serializable]
    internal class Puppet : NewFeatureModel {
        public string name;
        public bool saved;
        public bool slider;
        public List<Stop> stops = new List<Stop>();
        public float defaultX = 0;
        public float defaultY = 0;
        public bool enableIcon;
        public GuidTexture2d icon;
        
        [Serializable]
        public class Stop {
            public float x;
            public float y;
            public State state;
            public Stop(float x, float y, State state) {
                this.x = x;
                this.y = y;
                this.state = state;
            }
        }
    }
}