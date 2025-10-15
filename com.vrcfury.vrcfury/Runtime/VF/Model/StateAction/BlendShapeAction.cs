using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class BlendShapeAction : Action {
        public string blendShape;
        public float blendShapeValue = 100;
        public Renderer renderer;
        public bool allRenderers = true;

        public override bool Equals(Action other) => Equals(other as BlendShapeAction); 
        public bool Equals(BlendShapeAction other) {
            if (other == null) return false;
            if (blendShape != other.blendShape) return false;
            if (blendShapeValue != other.blendShapeValue) return false;
            if (allRenderers != other.allRenderers) return false;
            else if (!allRenderers && renderer != other.renderer) return false;
            return true;
        }
    }
}