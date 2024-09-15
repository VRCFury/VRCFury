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
    }
}