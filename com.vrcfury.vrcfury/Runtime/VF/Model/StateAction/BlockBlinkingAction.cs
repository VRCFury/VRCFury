using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class BlockBlinkingAction : Action {
        public override bool Equals(Action other) => Equals(other as BlockBlinkingAction); 
        public bool Equals(BlockBlinkingAction other) {
            if (other == null) return false;
            return true;
        }
    }
}