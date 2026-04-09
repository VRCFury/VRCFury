using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class Modes : NewFeatureModel {
        public string name;
        public bool saved;
        public bool securityEnabled;
        public List<Mode> modes = new List<Mode>();
        public List<GameObject> resetPhysbones = new List<GameObject>();
        
        [Serializable]
        public class Mode {
            public State state;
            public Mode() {
                this.state = new State();
            }
            public Mode(State state) {
                this.state = state;
            }
        }

#pragma warning disable 0612
        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            var tag = "mode_" + name.Replace(" ", "").Replace("/", "").Trim();
            var modeNum = 0;
            var output = new List<FeatureModel>();
            foreach (var mode in modes) {
                var toggle = new Toggle {
                    name = name + "/Mode " + (++modeNum),
                    saved = saved,
                    securityEnabled = securityEnabled,
                    resetPhysbones = new List<GameObject>(resetPhysbones),
                    state = mode.state,
                    enableExclusiveTag = true,
                    exclusiveTag = tag,
                    Version = 0
                };
                output.Add(toggle);
            }
            return output;
        }
#pragma warning restore 0612
    }
}