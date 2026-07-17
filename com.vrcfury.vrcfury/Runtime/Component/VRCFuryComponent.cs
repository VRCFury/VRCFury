using System;
using UnityEngine;
using VF.Upgradeable;
using VF.VrcfEditorOnly;

namespace VF.Component {
    internal abstract class VRCFuryComponent : VrcfUpgradeableMonoBehaviour, IVrcfEditorOnly {
        [NonSerialized] public GameObject gameObjectOverride;
        public new GameObject gameObject {
            get {
                if (gameObjectOverride != null) return gameObjectOverride;
                return base.gameObject;
            }
        }
        public new Transform transform {
            get {
                if (gameObjectOverride != null) return gameObjectOverride.transform;
                return base.transform;
            }
        }

        public static Action _OnValidate;

        private void OnValidate() {
            _OnValidate?.Invoke();
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}
