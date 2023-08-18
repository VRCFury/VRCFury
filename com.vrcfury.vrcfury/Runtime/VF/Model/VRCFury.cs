using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Component;
using VF.Model.Feature;
using VF.Upgradeable;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    [HelpURL("https://vrcfury.com")]
    public class VRCFury : VRCFuryComponent {
        [HideInInspector]
        public VRCFuryConfig config = new VRCFuryConfig();

        [Header("VRCFury failed to load")]
        public bool somethingIsBroken;
        
        public override bool Upgrade(int fromVersion) {
            var features = config.features;
            var didSomething = false;
            for (var i = 0; i < features.Count; i++) {
                if (features[i] is Modes modes) {
                    features.RemoveAt(i--);
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
                        features.Insert(++i, toggle);
                    }
                    didSomething = true;
                } else if (features[i] is LegacyFeatureModel legacy) {
                    features.RemoveAt(i--);
                    features.Insert(++i, legacy.CreateNewInstance());
                    didSomething = true;
                }
            }

            return didSomething;
        }

        public override int GetLatestVersion() {
            return 2;
        }
    }
    
    [Serializable]
    public class VRCFuryConfig {
        [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();
    }

    [Serializable]
    public class State {
        [SerializeReference] public List<Action> actions = new List<Action>();
    }
}
