using System;
using UnityEngine;

namespace VF.Upgradeable {
    [Serializable]
    public abstract class VrcfUpgradeable : IUpgradeable {
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
