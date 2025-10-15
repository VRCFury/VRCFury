using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class DisableGesturesAction : Action {
        public override bool Equals(Action other) => Equals(other as DisableGesturesAction); 
        public bool Equals(DisableGesturesAction other) {
            if (other == null) return false;
            return true;
        }
    }
}
