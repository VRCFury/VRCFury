using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class BlockVisemesAction : Action {
        public override bool Equals(Action other) => Equals(other as BlockVisemesAction); 
        public bool Equals(BlockVisemesAction other) {
            if (other == null) return false;
            return true;
        }
    }
}