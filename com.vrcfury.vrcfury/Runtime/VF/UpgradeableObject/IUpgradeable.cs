using UnityEngine;

namespace VF.Upgradeable {
    public interface IUpgradeable : ISerializationCallbackReceiver {
        bool Upgrade(int fromVersion);
        int GetLatestVersion();
        int Version { get; set; }
    }
}
