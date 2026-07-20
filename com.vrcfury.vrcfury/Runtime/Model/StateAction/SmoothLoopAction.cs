using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class SmoothLoopAction : Action {
        public State state1;
        public State state2;
        public float loopTime = 5;

        public override bool Equals(Action other) => Equals(other as SmoothLoopAction); 
        public bool Equals(SmoothLoopAction other) {
            if (other == null) return false;
            if (loopTime != other.loopTime) return false;
            if (!state1.Equals(other.state1)) return false;
            if (!state2.Equals(other.state2)) return false;
            return true;
        }
    }
}