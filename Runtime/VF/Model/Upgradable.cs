using UnityEngine;

namespace VF.Model {
    public abstract class Upgradable : ISerializationCallbackReceiver {
        public int version = -1;

        private int GetVersion() {
            return version < 0 ? GetLatestVersion() : version;
        }

        public void Upgrade() {
            var fromVersion = GetVersion();
            var latestVersion = GetLatestVersion();
            if (fromVersion != latestVersion) {
                Upgrade(fromVersion);
                version = latestVersion;
            }
        }

        public virtual void Upgrade(int fromVersion) {
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
        
        public void OnAfterDeserialize() {
            if (version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                version = 0;
            }
        }

        public void OnBeforeSerialize() {
            if (version < 0) {
                // Object was created fresh (not deserialized), so it's automatically the newest
                version = GetLatestVersion();
            }
        }
    }
}