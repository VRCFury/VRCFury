using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ShaderInventoryAction : Action {
        public Renderer renderer;
        public int slot = 1;

        public override bool Equals(Action other) => Equals(other as ShaderInventoryAction); 
        public bool Equals(ShaderInventoryAction other) {
            if (other == null) return false;
            if (renderer != other.renderer) return false;
            if (slot != other.slot) return false;
            return true;
        }
    }
}