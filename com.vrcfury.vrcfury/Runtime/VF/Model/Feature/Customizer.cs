using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class Customizer : NewFeatureModel {
        [SerializeReference] public List<CustomizerItem> items = new List<CustomizerItem>();
        
        [Serializable]
        public abstract class CustomizerItem { }

        public class MenuItem : CustomizerItem {
            public string key;
            public string title;
            public string path;
        }
        
        public class ClipItem : CustomizerItem {
            public string key;
            public string title;
            public GuidAnimationClip clip;
        }
    }
}