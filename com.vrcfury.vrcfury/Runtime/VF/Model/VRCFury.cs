using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Upgradeable;
using Action = VF.Model.StateAction.Action;

namespace VF.Model {
    [AddComponentMenu("")]
    [HelpURL("https://vrcfury.com")]
    public class VRCFury : VRCFuryComponent {
        
        [Header("VRCFury failed to load")]
        [Header("Something is really broken. Do not edit anything in here, or you may make it worse.")]
        [Header("You probably have script errors in the console caused by some other plugin. Please fix them.")]
        public bool somethingIsBroken;

        /**
         * Replaced by `content`. Only one feature can be set per vrcfury component. Every component MUST contain
         * a feature or it will be considered corrupted.
         */
        [Obsolete] public VRCFuryConfig config = new VRCFuryConfig();

        [SerializeReference] public FeatureModel content;

        public static bool RunningFakeUpgrade = false;

        public IEnumerable<FeatureModel> GetAllFeatures() {
            var output = new List<FeatureModel>();
#pragma warning disable 0612
            if (config?.features != null) {
                output.AddRange(config.features);
            }
#pragma warning restore 0612
            if (content != null) {
                output.Add(content);
            }
            return output;
        }
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
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
                } else if (features[i] is ObjectState os) {
                    features.RemoveAt(i--);

                    var apply = new ApplyDuringUpload();
                    apply.action = new State();
                    foreach (var s in os.states) {
                        if (s.obj == null) continue;
                        if (s.action == ObjectState.Action.DELETE) {
                            if (!RunningFakeUpgrade) {
                                var vrcf = s.obj.AddComponent<VRCFury>();
                                vrcf.content = new DeleteDuringUpload();
                            }
                        } else if (s.action == ObjectState.Action.ACTIVATE) {
                            apply.action.actions.Add(new ObjectToggleAction() {
                                mode = ObjectToggleAction.Mode.TurnOn,
                                obj = s.obj
                            });
                        } else if (s.action == ObjectState.Action.DEACTIVATE) {
                            apply.action.actions.Add(new ObjectToggleAction() {
                                mode = ObjectToggleAction.Mode.TurnOff,
                                obj = s.obj
                            });
                        }
                    }

                    if (apply.action.actions.Count > 0) {
                        features.Insert(++i, apply);
                    }

                    didSomething = true;
                } else if (features[i] is LegacyFeatureModel legacy) {
                    features.RemoveAt(i--);
                    features.Insert(++i, legacy.CreateNewInstance());
                    didSomething = true;
                } else if (features[i] is BlendshapeOptimizer opt) {
                    if (opt.keepMmdShapes && !RunningFakeUpgrade) {
                        var hasMmdCompat = gameObject.GetComponents<VRCFury>()
                            .Where(c => c != null)
                            .SelectMany(c => c.GetAllFeatures())
                            .Any(feature => feature is MmdCompatibility);
                        if (!hasMmdCompat) {
                            features.Insert(++i, new MmdCompatibility());
                        }
                        opt.keepMmdShapes = false;
                        didSomething = true;
                    }
                }
            }

            if (content == null && features.Count == 1) {
                content = features[0];
                features.Clear();
                didSomething = true;
            } else if (features.Count >= 1) {
                foreach (var f in features) {
                    var newComponent = gameObject.AddComponent<VRCFury>();
                    newComponent.content = f;
                }
                features.Clear();
                didSomething = true;
            }
            if (content == null) {
                DestroyImmediate(this, true);
            }

            return didSomething;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 3;
        }
    }
    
    [Serializable]
    public class VRCFuryConfig {
        [SerializeReference] public List<FeatureModel> features = new List<FeatureModel>();
    }

    [Serializable]
    public class State {
        [SerializeReference] public List<Action> actions = new List<Action>();
        public bool ResetMePlease2;
    }
}
