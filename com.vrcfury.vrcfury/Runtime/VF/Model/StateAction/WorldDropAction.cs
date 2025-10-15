using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class WorldDropAction : Action {
        public GameObject obj;
        public override bool Equals(Action other) => Equals(other as WorldDropAction); 
        public bool Equals(WorldDropAction other) {
            if (other == null) return false;
            if (obj != other.obj) return false;
            return true;
        }
    }
}
