using System;
using UnityEngine;
using VF.Upgradeable;

namespace VF.Component {
    public abstract class VRCFuryComponent : VrcfUpgradeableMonoBehaviour, IVrcfEditorOnly {
        [NonSerialized] public GameObject gameObjectOverride;
        public new GameObject gameObject => gameObjectOverride != null ? gameObjectOverride : base.gameObject;

        public new Transform transform => gameObjectOverride != null ? gameObjectOverride.transform : base.transform;
        
        public override int GetLatestVersion() {
            return 1;
        }
    }
}
