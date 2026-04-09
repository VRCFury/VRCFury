using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class SmoothLoopAction : Action {
        public State state1;
        public State state2;
        public float loopTime = 5;
    }
}