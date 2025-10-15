using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class FlipbookAction : Action {
        [Obsolete] public GameObject obj;
        public Renderer renderer;
        public int frame;

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (obj != null) {
                    renderer = obj.GetComponent<Renderer>();
                }
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }

        public override bool Equals(Action other) => Equals(other as FlipbookAction); 
        public bool Equals(FlipbookAction other) {
            if (other == null) return false;
            if (renderer != other.renderer) return false;
            if (frame != other.frame) return false;
            return true;
        }
    }
}