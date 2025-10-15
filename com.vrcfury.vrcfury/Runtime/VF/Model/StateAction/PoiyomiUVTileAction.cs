using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class PoiyomiUVTileAction : Action {
        public Renderer renderer;
        public int row = 0;
        public int column = 0;
        public bool dissolve = false;
        public string renamedMaterial = "";

        public override bool Equals(Action other) => Equals(other as PoiyomiUVTileAction); 
        public bool Equals(PoiyomiUVTileAction other) {
            if (other == null) return false;
            if (renderer != other.renderer) return false;
            if (row != other.row) return false;
            if (column != other.column) return false;
            if (dissolve != other.dissolve) return false;
            if (renamedMaterial != other.renamedMaterial) return false;
            return true;
        }

    }
}