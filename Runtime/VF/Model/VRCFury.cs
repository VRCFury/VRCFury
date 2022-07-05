using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model.Feature;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    public class VRCFury : MonoBehaviour {
        [HideInInspector]
        public VRCFuryConfig config = new VRCFuryConfig();

        [Header("VRCFury failed to load")]
        public bool somethingIsBroken;
    }
    
    [Serializable]
    public class VRCFuryConfig {
        [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();
    }

    [Serializable]
    public class State {
        [SerializeReference] public List<Action> actions = new List<Action>();
        public bool isEmpty() {
            return actions.Count == 0;
        }
    }
}
