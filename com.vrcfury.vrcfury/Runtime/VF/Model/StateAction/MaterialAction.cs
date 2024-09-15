using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class MaterialAction : Action {
        [Obsolete] public GameObject obj;
        public Renderer renderer;
        public int materialIndex = 0;
        public GuidMaterial mat = null;
        
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
    }
}