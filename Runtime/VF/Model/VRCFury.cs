using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Model.Feature;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {

    public class VRCFury : VRCFuryComponent {
        [HideInInspector]
        public VRCFuryConfig config = new VRCFuryConfig();

        [Header("VRCFury failed to load")]
        public bool somethingIsBroken;
    }
    
    [Serializable]
    public class VRCFuryConfig {
        [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();

        public void Upgrade() {
            for (var i = 0; i < features.Count; i++) {
                if (!(features[i] is Modes modes)) continue;
                features.RemoveAt(i);
                var tag = "mode_" + modes.name.Replace(" ", "").Replace("/", "").Trim();
                var modeNum = 0;
                foreach (var mode in modes.modes) {
                    var toggle = new Toggle();
                    toggle.name = modes.name + "/Mode " + (++modeNum);
                    toggle.saved = modes.saved;
                    toggle.securityEnabled = modes.securityEnabled;
                    toggle.resetPhysbones = new List<GameObject>(modes.resetPhysbones);
                    toggle.state = mode.state;
                    toggle.enableExclusiveTag = true;
                    toggle.exclusiveTag = tag;
                    features.Insert(i, toggle);
                    i++;
                }
            }
            foreach (var f in features) {
                f?.Upgrade();
            }
        }
    }

    [Serializable]
    public class State {
        [SerializeReference] public List<Action> actions = new List<Action>();
        public bool IsEmpty() {
            return actions.Count == 0 || actions.All(a => a.IsEmpty());
        }
    }
}
