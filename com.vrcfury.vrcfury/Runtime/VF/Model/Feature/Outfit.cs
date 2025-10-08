using System;
using System.Collections.Generic;

namespace VF.Model.Feature
{

    [Serializable]
    internal class Outfit : FeatureModel
    {
        public string name = "";

        public string[] toggleOn;
        public string[] toggleOff;

        public List<Action> actions = new List<Action>();
    }
}
