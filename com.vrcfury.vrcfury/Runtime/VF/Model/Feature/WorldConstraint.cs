using System;
using System.Collections.Generic;
using VF.Model.StateAction;

namespace VF.Model.Feature {
    [Serializable]
    internal class WorldConstraint : NewFeatureModel {
        public string menuPath;
        
        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            return new FeatureModel[] {
                new Toggle() {
                    name = menuPath,
                    state = new State() {
                        actions = {
                            new WorldDropAction() {
                                obj = request.gameObject,
                            }
                        }
                    }
                }
            };
        }
    }
}
