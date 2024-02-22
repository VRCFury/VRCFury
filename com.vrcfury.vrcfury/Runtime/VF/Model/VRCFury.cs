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
            IEnumerable<FeatureModel> Migrate(FeatureModel input) {
                var migrated = input.Migrate(new FeatureModel.MigrateRequest {
                    fakeUpgrade = RunningFakeUpgrade,
                    gameObject = gameObject
                });

                // Recursively upgrade the migrated features
                migrated = migrated.SelectMany(newFeature => {
                    if (newFeature == input) return new[] { newFeature };
                    IUpgradeableUtility.UpgradeRecursive(newFeature);
                    return Migrate(newFeature);
                }).ToList();

                return migrated;
            }

            var migrated = GetAllFeatures().SelectMany(Migrate).ToList();
            if (migrated.Count == 0) {
                DestroyImmediate(this, true);
                return false;
            } else if (migrated.Count == 1 && migrated[0] != content) {
                content = migrated[0];
                config?.features?.Clear();
                return true;
            } else if (migrated.Count > 1) {
                foreach (var f in migrated) {
                    var newComponent = gameObject.AddComponent<VRCFury>();
                    newComponent.content = f;
                }
                DestroyImmediate(this, true);
                return false;
            }

            return false;
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
