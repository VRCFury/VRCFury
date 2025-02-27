using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFStateMachine : VFBehaviourContainer {
        private readonly VFLayer layer;
        private readonly AnimatorStateMachine sm;
        
        public VFStateMachine(VFLayer layer, AnimatorStateMachine sm) {
            this.layer = layer;
            this.sm = sm;
        }

        public StateMachineBehaviour[] behaviours {
            get => sm.behaviours;
            set => sm.behaviours = value;
        }

        public string name => sm.name;
        
        public ICollection<VFState> states => sm.states
            .Select(c => new VFState(layer, c, sm))
            .ToArray();
    }
}
