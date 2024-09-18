using System;
using UnityEngine;
using VF.Upgradeable;

namespace VF.Model.Feature {
    [Serializable]
    internal abstract class NewFeatureModel : FeatureModel, IUpgradeable {
        [SerializeField] private int version = -1;
        public int Version { get => version; set => version = value; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { this.IUpgradeableOnAfterDeserialize(); }
        void ISerializationCallbackReceiver.OnBeforeSerialize() { this.IUpgradeableOnBeforeSerialize(); }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
    }
}