using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class BlendShapeAction : Action {
        public string blendShape;
        public float blendShapeValue = 100;
        public Renderer renderer;
        public bool allRenderers = true;
    }
}