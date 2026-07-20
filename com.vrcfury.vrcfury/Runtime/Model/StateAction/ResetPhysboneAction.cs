using System;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ResetPhysboneAction : Action {
        public VRCPhysBone physBone;

        public override bool Equals(Action other) => Equals(other as ResetPhysboneAction); 
        public bool Equals(ResetPhysboneAction other) {
            if (other == null) return false;
            if (physBone != other.physBone) return false;
            return true;
        }
    }
}