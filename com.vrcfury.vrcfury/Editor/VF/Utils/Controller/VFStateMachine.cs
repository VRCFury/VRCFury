using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFStateMachine : VFBehaviourContainer {
        private readonly AnimatorStateMachine sm;

        public VFStateMachine(AnimatorStateMachine sm) {
            this.sm = sm;
        }

        public StateMachineBehaviour[] behaviours {
            get => sm.behaviours;
            set => sm.behaviours = value;
        }
    }
}
