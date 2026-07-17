using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ScaleAction : Action {
        public GameObject obj;
        public float scale = 1;

        public override bool Equals(Action other) => Equals(other as ScaleAction); 
        public bool Equals(ScaleAction other) {
            if (other == null) return false;
            if (obj != other.obj) return false;
            if (scale != other.scale) return false;
            return true;
        }
    }
}