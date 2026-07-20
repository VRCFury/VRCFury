using System;
using VF.Component;

namespace VF.Model.StateAction {
    [Serializable]
    internal class SpsOnAction : Action {
        public VRCFuryHapticPlug target;

        public override bool Equals(Action other) => Equals(other as SpsOnAction); 
        public bool Equals(SpsOnAction other) {
            if (other == null) return false;
            if (target != other.target) return false;
            return true;
        }
    }
}