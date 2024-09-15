using System;
using System.Collections.Generic;

namespace VF.Model.Feature {
    [Serializable]
    internal class MmdCompatibility : NewFeatureModel {
        public List<DisableLayer> disableLayers = new List<DisableLayer>();
        public string globalParam;

        [Serializable]
        public class DisableLayer {
            public string name;
        }
    }
}