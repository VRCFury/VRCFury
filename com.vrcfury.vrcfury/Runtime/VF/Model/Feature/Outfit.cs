using System;
using System.Collections.Generic;

namespace VF.Model.Feature
{
    [Serializable]
    internal class Outfit : NewFeatureModel
    {
        public string name = "";

        public List<string> toggleOn = new List<string>();
        public List<string> toggleOff = new List<string>();
        public bool allOff = false;
    }
}