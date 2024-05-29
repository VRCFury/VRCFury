using UnityEngine;

namespace VF.Upgradeable {
    // Temporarily public for SPS Configurator
    public abstract class VrcfUpgradeableMonoBehaviour : MonoBehaviour, IUpgradeable {
        [SerializeField] private int version = -1;
        public string unityVersion;
        public string vrcfuryVersion;

        public static string currentVrcfVersion { private get; set; }

        public int Version { get => version; set => version = value; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { this.IUpgradeableOnAfterDeserialize(); }

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            unityVersion = Application.unityVersion;
            vrcfuryVersion = currentVrcfVersion;
            this.IUpgradeableOnBeforeSerialize();
        }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
    }
}
