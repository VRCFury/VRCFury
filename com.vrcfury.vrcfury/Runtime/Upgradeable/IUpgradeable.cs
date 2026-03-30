using UnityEngine;

namespace VF.Upgradeable {
    internal interface IUpgradeable : ISerializationCallbackReceiver {
        bool Upgrade(int fromVersion);
        int GetLatestVersion();
        int Version { get; set; }
    }
}
