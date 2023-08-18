using System;
using UnityEngine;
using VF.Upgradeable;

namespace VF.Component {
    public abstract class VRCFuryComponent : VrcfUpgradeableMonoBehaviour, IVrcfEditorOnly {
        [NonSerialized] public GameObject gameObjectOverride;
        public new GameObject gameObject {
            get {
                if (gameObjectOverride != null) return gameObjectOverride;
                return base.gameObject;
            }
        }
        
        public override int GetLatestVersion() {
            return 1;
        }
    }
}
