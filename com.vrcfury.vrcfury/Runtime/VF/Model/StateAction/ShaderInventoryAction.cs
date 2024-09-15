using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ShaderInventoryAction : Action {
        public Renderer renderer;
        public int slot = 1;
    }
}