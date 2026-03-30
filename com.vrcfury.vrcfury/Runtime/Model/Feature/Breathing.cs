using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model.StateAction;

namespace VF.Model.Feature {
    [Serializable]
    internal class Breathing : NewFeatureModel {
        public State inState;
        public State outState;

        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                if (obj != null) {
                    inState.actions.Add(new ScaleAction { obj = obj, scale = scaleMin });
                    outState.actions.Add(new ScaleAction { obj = obj, scale = scaleMax });
                    obj = null;
                }

                if (!string.IsNullOrWhiteSpace(blendshape)) {
                    inState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 0 });
                    outState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 100 });
                    blendshape = null;
                }
            }
            return false;
        }
        public override int GetLatestVersion() {
            return 1;
        }

        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            return new FeatureModel[] {
                new Toggle() {
                    name = "Breathing",
                    defaultOn = true,
                    state = new State() {
                        actions = {
                            new SmoothLoopAction() {
                                state1 = outState,
                                state2 = inState,
                                loopTime = 5,
                            }
                        }
                    }
                }
            };
        }

        // legacy
        public GameObject obj;
        public string blendshape;
        public float scaleMin;
        public float scaleMax;
    }
}