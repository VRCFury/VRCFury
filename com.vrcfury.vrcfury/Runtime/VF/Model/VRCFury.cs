using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Model.Feature;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    [HelpURL("https://vrcfury.com")]
    public class VRCFury : VRCFuryComponent {
        [HideInInspector]
        public VRCFuryConfig config = new VRCFuryConfig();

        [Header("VRCFury failed to load")]
        public bool somethingIsBroken;
        
        protected override void UpgradeAlways() {
#if UNITY_EDITOR
            var features = config.features;
            foreach (var f in features) {
                if (f is NewFeatureModel newf) {
                    if (newf.Upgrade()) {
                        EditorUtility.SetDirty(this);
                    }
                }
            }
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
                    EditorUtility.SetDirty(this);
                } else if (features[i] is LegacyFeatureModel legacy) {
                    features.RemoveAt(i--);
                    features.Insert(++i, legacy.CreateNewInstance());
                    EditorUtility.SetDirty(this);
                } /*else if (features[i] is LegacyFeatureModel2 legacy2) {
                    features.RemoveAt(i--);
                    legacy2.CreateNewInstance(gameObject);
                    EditorUtility.SetDirty(this);
                    EditorUtility.SetDirty(gameObject);
                }*/
            }
#endif
        }

        protected override int GetLatestVersion() {
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
        public bool IsEmpty(){
            return actions.Count == 0;
        }
    }
}
