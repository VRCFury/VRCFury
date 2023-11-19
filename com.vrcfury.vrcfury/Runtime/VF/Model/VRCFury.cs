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
    [HelpURL("https://vrcfury.com")]
    public class VRCFury : VRCFuryComponent {

        public VRCFuryConfig config = new VRCFuryConfig();

        [Header("VRCFury failed to load")]
        public bool somethingIsBroken;

        public static bool RunningFakeUpgrade = false;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            var features = config.features;
            var didSomething = false;
            for (var i = 0; i < features.Count; i++) {
                switch (features[i]) {
                    case Modes modes: {
                        features.RemoveAt(i--);
                        var exclusiveTag = "mode_" + modes.name.Replace(" ", "").Replace("/", "").Trim();
                        var modeNum = 0;
                        foreach (var toggle in modes.modes.Select(mode => new Toggle {
                                     name = modes.name + "/Mode " + ++modeNum,
                                     saved = modes.saved,
                                     securityEnabled = modes.securityEnabled,
                                     resetPhysbones = new List<GameObject>(modes.resetPhysbones),
                                     state = mode.state,
                                     enableExclusiveTag = true,
                                     exclusiveTag = exclusiveTag
                                 })) {
                            features.Insert(++i, toggle);
                        }

                        didSomething = true;
                        break;
                    }
                    case ObjectState os:  {
                        features.RemoveAt(i--);

                        var apply = new ApplyDuringUpload {
                            action = new State()
                        };
                        foreach (var s in os.states.Where(s => s.obj != null))
                        {
                            switch (s.action)
                            {
                                case ObjectState.Action.DELETE: {
                                    if (!RunningFakeUpgrade) {
                                        var vrcf = s.obj.AddComponent<VRCFury>();
                                        vrcf.config.features.Add(new DeleteDuringUpload());
                                    }

                                    break;
                                }
                                case ObjectState.Action.ACTIVATE:
                                    apply.action.actions.Add(new ObjectToggleAction {
                                        mode = ObjectToggleAction.Mode.TurnOn,
                                        obj = s.obj
                                    });
                                    break;
                                case ObjectState.Action.DEACTIVATE:
                                    apply.action.actions.Add(new ObjectToggleAction {
                                        mode = ObjectToggleAction.Mode.TurnOff,
                                        obj = s.obj
                                    });
                                    break;
                            }
                        }

                        if (apply.action.actions.Count > 0) {
                            features.Insert(++i, apply);
                        }

                        didSomething = true;
                        break;
                    }
                    case LegacyFeatureModel legacy:
                        features.RemoveAt(i--);
                        features.Insert(++i, legacy.CreateNewInstance());
                        didSomething = true;
                        break;
                    case BlendshapeOptimizer opt: {
                        if (opt.keepMmdShapes && !RunningFakeUpgrade) {
                            var hasMmdCompat = gameObject.GetComponents<VRCFury>()
                                .Where(c => c != null && c.config?.features != null)
                                .SelectMany(c => c.config.features)
                                .Any(feature => feature is MmdCompatibility);
                            if (!hasMmdCompat) {
                                features.Insert(++i, new MmdCompatibility());
                            }
                            opt.keepMmdShapes = false;
                            didSomething = true;
                        }

                        break;
                    }
                }
            }

            return didSomething;
#pragma warning restore 0612
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
