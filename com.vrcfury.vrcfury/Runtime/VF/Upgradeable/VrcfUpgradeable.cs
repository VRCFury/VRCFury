using System;
using UnityEngine;

namespace VF.Upgradeable {
    [Serializable]
    internal abstract class VrcfUpgradeable : IUpgradeable {
        [SerializeField] private int version = -1;
        public int Version { get => version; set => version = value; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { this.IUpgradeableOnAfterDeserialize(); }
        void ISerializationCallbackReceiver.OnBeforeSerialize() { PreSerialize(); this.IUpgradeableOnBeforeSerialize(); }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }

        protected virtual void PreSerialize() {
            
        }
    }
}
