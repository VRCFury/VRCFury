using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class FxFloatAction : Action {
        public string name;
        public float value = 1;

        public override bool Equals(Action other) => Equals(other as FxFloatAction); 
        public bool Equals(FxFloatAction other) {
            if (other == null) return false;
            if (name != other.name) return false;
            if (value != other.value) return false;
            return true;
        }
    }
}